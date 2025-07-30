using MediatR;
using TournamentService.Infrastructure.Repositories;

namespace TournamentService.Application.Queries;

public record GetUpcomingTournamentsQuery : IRequest<IEnumerable<UpcomingTournamentDto>>;
public record UpcomingTournamentDto(Guid Id, string Name, DateTime StartsAt, int SlotsLeft);

public class GetUpcomingTournamentsQueryHandler : IRequestHandler<GetUpcomingTournamentsQuery, IEnumerable<UpcomingTournamentDto>>
{
    private readonly ITournamentRepository _repo;
    public GetUpcomingTournamentsQueryHandler(ITournamentRepository repo) => _repo = repo;

    public async Task<IEnumerable<UpcomingTournamentDto>> Handle(GetUpcomingTournamentsQuery request, CancellationToken cancellationToken)
    {
        var list = await _repo.FilterUpcomingAsync(cancellationToken);
        return list.Select(t => new UpcomingTournamentDto(t.Id, t.Name, t.StartsAt, t.MaxPlayers - t.Participants.Count));
    }
}