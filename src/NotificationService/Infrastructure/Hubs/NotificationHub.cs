using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace NotificationService.Infrastructure.Hubs;

public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        // Get the user ID from claims or query string
        var userId = GetUserId(Context);
        
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, userId);
            _logger.LogInformation("User {UserId} connected to notification hub with connection {ConnectionId}", 
                userId, Context.ConnectionId);
        }
        else
        {
            _logger.LogWarning("Connection {ConnectionId} has no userId, notifications won't be received", 
                Context.ConnectionId);
        }
        
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId(Context);
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);
            _logger.LogInformation("User {UserId} disconnected from notification hub", userId);
        }
        
        await base.OnDisconnectedAsync(exception);
    }
    
    private string? GetUserId(HubCallerContext context)
    {
        // First try to get from JWT claims if auth is set up
        var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        // If not available, try to get from query string (for development/testing)
        if (string.IsNullOrEmpty(userId) && context.GetHttpContext() is HttpContext httpContext)
        {
            userId = httpContext.Request.Query["userId"].FirstOrDefault();
        }
        
        return userId;
    }
}