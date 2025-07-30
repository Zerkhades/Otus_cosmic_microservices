using MediatR;
using Microsoft.AspNetCore.SignalR;
using NotificationService.Domains;
using NotificationService.Infrastructure.Hubs;
using NotificationService.Infrastructure.Repositories;

namespace NotificationService.Application.Commands;

public record EnqueueNotificationCommand(Guid RecipientId, string Type, string PayloadJson) : IRequest<Guid>;

public class EnqueueNotificationCommandHandler : IRequestHandler<EnqueueNotificationCommand, Guid>
{
    private readonly INotificationRepository _repo;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<EnqueueNotificationCommandHandler> _logger;

    public EnqueueNotificationCommandHandler(
        INotificationRepository repo,
        IHubContext<NotificationHub> hubContext,
        ILogger<EnqueueNotificationCommandHandler> logger)
    {
        _repo = repo;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<Guid> Handle(EnqueueNotificationCommand request, CancellationToken cancellationToken)
    {
        var entity = new Notification
        {
            RecipientId = request.RecipientId,
            Type = request.Type,
            PayloadJson = request.PayloadJson
        };
        
        try
        {
            // Save notification to database (outbox pattern)
            await _repo.AddAsync(entity, cancellationToken);
            await _repo.SaveChangesAsync(cancellationToken);
            
            // Attempt real-time delivery via SignalR
            await _hubContext.Clients.Group(request.RecipientId.ToString())
                .SendAsync("notify", request.Type, request.PayloadJson, cancellationToken);
                
            // Mark as sent if delivered successfully
            await _repo.MarkAsSentAsync(entity.Id, cancellationToken);
            
            _logger.LogInformation("Notification {Id} of type {Type} enqueued for recipient {RecipientId}", 
                entity.Id, request.Type, request.RecipientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enqueuing notification for recipient {RecipientId}", request.RecipientId);
            // The notification is still saved in the DB and can be retrieved later
        }
        
        return entity.Id;
    }
}