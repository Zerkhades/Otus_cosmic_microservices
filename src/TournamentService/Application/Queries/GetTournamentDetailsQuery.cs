using MediatR;
using TournamentService.Domains;
using TournamentService.Infrastructure.Repositories;

namespace TournamentService.Application.Queries;

public record GetTournamentDetailsQuery(Guid Id) : IRequest<Tournament?>;

public class GetTournamentDetailsQueryHandler : IRequestHandler<GetTournamentDetailsQuery, Tournament?>
{
    private readonly ITournamentRepository _repo;
    public GetTournamentDetailsQueryHandler(ITournamentRepository repo) => _repo = repo;

    public Task<Tournament?> Handle(GetTournamentDetailsQuery request, CancellationToken cancellationToken)
        => _repo.FindAsync(request.Id, cancellationToken);
}