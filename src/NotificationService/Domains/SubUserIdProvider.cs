using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace NotificationService.Domains
{
    public sealed class SubUserIdProvider : IUserIdProvider
    {
        public string? GetUserId(HubConnectionContext connection) =>
            connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? connection.User?.FindFirst("sub")?.Value;
    }
}
