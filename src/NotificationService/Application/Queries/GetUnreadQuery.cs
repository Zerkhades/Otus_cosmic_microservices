using MediatR;
using NotificationService.Domains;
using NotificationService.Infrastructure.Repositories;

namespace NotificationService.Application.Queries;

public record GetUnreadQuery(Guid RecipientId) : IRequest<IEnumerable<Notification>>;

public class GetUnreadQueryHandler : IRequestHandler<GetUnreadQuery, IEnumerable<Notification>>
{
    private readonly INotificationRepository _repo;
    private readonly ILogger<GetUnreadQueryHandler> _logger;
    
    public GetUnreadQueryHandler(INotificationRepository repo, ILogger<GetUnreadQueryHandler> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task<IEnumerable<Notification>> Handle(GetUnreadQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving unread notifications for recipient {RecipientId}", request.RecipientId);
        return await _repo.GetUnreadAsync(request.RecipientId, cancellationToken);
    }
}