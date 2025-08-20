using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Features.Authentication;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using NotificationService.Infrastructure.Hubs;
using System;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;

namespace NotificationServiceTests;

public class NotificationHubTests
{
    [Fact]
    public async Task OnConnectedAsync_AddsToGroup_WhenUserIdFromClaim()
    {
        // Arrange
        var logger = new Mock<ILogger<NotificationHub>>();
        var hub = new NotificationHub(logger.Object);

        var userId = "user-123";
        var context = CreateHubCallerContext(
            connectionId: "conn-1",
            user: CreatePrincipalWithNameIdentifier(userId)
        );

        var groups = new Mock<IGroupManager>(MockBehavior.Strict);
        groups.Setup(g => g.AddToGroupAsync("conn-1", userId, default))
              .Returns(Task.CompletedTask)
              .Verifiable();

        AttachToHub(hub, context.Object, groups.Object);

        // Act
        await hub.OnConnectedAsync();

        // Assert
        groups.Verify();
        logger.VerifyLog(LogLevel.Information, Times.Once());
        logger.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OnConnectedAsync_UsesQueryString_WhenClaimMissing()
    {
        // Arrange
        var logger = new Mock<ILogger<NotificationHub>>();
        var hub = new NotificationHub(logger.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString("?userId=query-user");

        var context = CreateHubCallerContext(
            connectionId: "conn-2",
            user: new ClaimsPrincipal(new ClaimsIdentity()),           // без NameIdentifier
            httpContext: httpContext
        );

        var groups = new Mock<IGroupManager>(MockBehavior.Strict);
        groups.Setup(g => g.AddToGroupAsync("conn-2", "query-user", default))
              .Returns(Task.CompletedTask)
              .Verifiable();

        AttachToHub(hub, context.Object, groups.Object);

        // Act
        await hub.OnConnectedAsync();

        // Assert
        groups.Verify();
        logger.VerifyLog(LogLevel.Information, Times.Once());
        logger.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OnConnectedAsync_LogsWarning_WhenNoUserId()
    {
        // Arrange
        var logger = new Mock<ILogger<NotificationHub>>();
        var hub = new NotificationHub(logger.Object);

        var httpContext = new DefaultHttpContext(); // без userId в query
        var context = CreateHubCallerContext(
            connectionId: "conn-3",
            user: new ClaimsPrincipal(new ClaimsIdentity()),
            httpContext: httpContext
        );

        var groups = new Mock<IGroupManager>(MockBehavior.Strict);
        // не должно быть вызовов AddToGroupAsync
        AttachToHub(hub, context.Object, groups.Object);

        // Act
        await hub.OnConnectedAsync();

        // Assert
        groups.VerifyNoOtherCalls();
        logger.VerifyLog(LogLevel.Warning, Times.Once());
        logger.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OnDisconnectedAsync_RemovesFromGroup_WhenUserIdPresent()
    {
        // Arrange
        var logger = new Mock<ILogger<NotificationHub>>();
        var hub = new NotificationHub(logger.Object);

        var userId = "user-456";
        var context = CreateHubCallerContext(
            connectionId: "conn-4",
            user: CreatePrincipalWithNameIdentifier(userId)
        );

        var groups = new Mock<IGroupManager>(MockBehavior.Strict);
        groups.Setup(g => g.RemoveFromGroupAsync("conn-4", userId, default))
              .Returns(Task.CompletedTask)
              .Verifiable();

        AttachToHub(hub, context.Object, groups.Object);

        // Act
        await hub.OnDisconnectedAsync(null);

        // Assert
        groups.Verify();
        logger.VerifyLog(LogLevel.Information, Times.Once());
        logger.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OnDisconnectedAsync_DoesNothing_WhenNoUserId()
    {
        // Arrange
        var logger = new Mock<ILogger<NotificationHub>>();
        var hub = new NotificationHub(logger.Object);

        var context = CreateHubCallerContext(
            connectionId: "conn-5",
            user: new ClaimsPrincipal(new ClaimsIdentity())
        );

        var groups = new Mock<IGroupManager>(MockBehavior.Strict);
        AttachToHub(hub, context.Object, groups.Object);

        // Act
        await hub.OnDisconnectedAsync(new Exception("boom"));

        // Assert
        groups.VerifyNoOtherCalls();
        // Нет логирования Info (и Warning тоже не обязан в Disconnect)
        logger.VerifyNoOtherCalls();
    }

    // ---------- helpers ----------

    private static ClaimsPrincipal CreatePrincipalWithNameIdentifier(string userId) =>
        new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId)
        }, authenticationType: "TestAuth"));

    /// <summary>
    /// Создаёт замоканный HubCallerContext с заданными полями и при необходимости HttpContext в Features.
    /// </summary>
    private static Mock<HubCallerContext> CreateHubCallerContext(
        string connectionId,
        ClaimsPrincipal user,
        HttpContext? httpContext = null)
    {
        var context = new Mock<HubCallerContext>();

        context.SetupGet(c => c.ConnectionId).Returns(connectionId);
        context.SetupGet(c => c.User).Returns(user);

        var features = new FeatureCollection();
        if (httpContext != null)
        {
            features.Set<IHttpContextFeature>(new TestHttpContextFeature { HttpContext = httpContext });
        }
        context.SetupGet(c => c.Features).Returns(features);

        return context;
    }

    /// <summary>
    /// «Инъекция» Context и Groups в Hub через рефлексию (у этих свойств internal setter).
    /// </summary>
    private static void AttachToHub(Hub hub, HubCallerContext context, IGroupManager groups)
    {
        var hubType = typeof(Hub);

        var contextProp = hubType.GetProperty(nameof(Hub.Context))!;
        contextProp.GetSetMethod(nonPublic: true)!.Invoke(hub, new object?[] { context });

        var groupsProp = hubType.GetProperty(nameof(Hub.Groups))!;
        groupsProp.GetSetMethod(nonPublic: true)!.Invoke(hub, new object?[] { groups });

        // Clients нам не нужен для этих тестов.
    }

    /// <summary>
    /// Простая реализация IHttpContextFeature для помещения HttpContext в FeatureCollection.
    /// </summary>
    private sealed class TestHttpContextFeature : IHttpContextFeature
    {
        public HttpContext HttpContext { get; set; } = default!;
    }
}

/// <summary>
/// Утилита для удобной проверки вызовов ILogger c конкретным уровнем.
/// </summary>
internal static class LoggerMoqExtensions
{
    public static void VerifyLog<T>(this Mock<ILogger<T>> logger, LogLevel level, Times times)
    {
        logger.Verify(x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }
}
