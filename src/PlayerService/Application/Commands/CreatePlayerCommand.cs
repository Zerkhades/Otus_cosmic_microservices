using MediatR;
using PlayerService.Domains;
using PlayerService.Infrastructure.Repositories;

namespace PlayerService.Application.Commands;

public record CreatePlayerCommand(string UserName) : IRequest<Guid>;

public class CreatePlayerCommandHandler : IRequestHandler<CreatePlayerCommand, Guid>
{
    private readonly IPlayerRepository _repo;
    private readonly IKafkaProducerWrapper _producer;

    public CreatePlayerCommandHandler(IPlayerRepository repo, IKafkaProducerWrapper producer)
    {
        _repo = repo;
        _producer = producer;
    }

    public async Task<Guid> Handle(CreatePlayerCommand request, CancellationToken cancellationToken)
    {
        var player = new Player(request.UserName);
        await _repo.AddAsync(player, cancellationToken);
        await _repo.SaveChangesAsync(cancellationToken);

        await _producer.PublishAsync("player.registered", player.Id.ToString(), cancellationToken);
        return player.Id;
    }
}