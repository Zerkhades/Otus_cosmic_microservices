using System.Security.Claims;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationService.Domains;
using Xunit;

namespace NotificationServiceTests;

public class SubUserIdProviderTests
{
    [Fact]
    public void GetUserId_ReturnsNameIdentifier_WhenPresent()
    {
        // Arrange
        var provider = new SubUserIdProvider();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "name-id-1"),
            new Claim("sub", "sub-id-1")
        }, "TestAuth"));

        var connection = new TestHubConnectionContext(principal);

        // Act
        var result = provider.GetUserId(connection);

        // Assert
        Assert.Equal("name-id-1", result);
    }

    [Fact]
    public void GetUserId_FallsBackToSub_WhenNameIdentifierMissing()
    {
        // Arrange
        var provider = new SubUserIdProvider();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", "sub-id-2")
        }, "TestAuth"));

        var connection = new TestHubConnectionContext(principal);

        // Act
        var result = provider.GetUserId(connection);

        // Assert
        Assert.Equal("sub-id-2", result);
    }

    [Fact]
    public void GetUserId_ReturnsNull_WhenNoRelevantClaims()
    {
        // Arrange
        var provider = new SubUserIdProvider();
        var principal = new ClaimsPrincipal(new ClaimsIdentity()); // пусто

        var connection = new TestHubConnectionContext(principal);

        // Act
        var result = provider.GetUserId(connection);

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// Минимальный тестовый контекст: прокидываем конструктор и подменяем User.
    /// </summary>
    private sealed class TestHubConnectionContext : HubConnectionContext
    {
        private readonly ClaimsPrincipal _user;

        public TestHubConnectionContext(ClaimsPrincipal user)
            : base(new DefaultConnectionContext(),
                   new HubConnectionContextOptions { KeepAliveInterval = TimeSpan.FromSeconds(15) },
                   NullLoggerFactory.Instance)
        {
            _user = user;
        }

        public override ClaimsPrincipal? User => _user;
    }
}
