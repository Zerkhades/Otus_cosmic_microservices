using MediatR;
using Microsoft.EntityFrameworkCore;
using PlayerService.Infrastructure.Persistence;

namespace PlayerService.Application.Queries;

public record GetPlayerQuery(Guid Id) : IRequest<PlayerDto?>;

public record PlayerDto(Guid Id, string UserName, int Rating);

public class GetPlayerQueryHandler : IRequestHandler<GetPlayerQuery, PlayerDto?>
{
    private readonly AppDbContext _db;

    public GetPlayerQueryHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PlayerDto?> Handle(GetPlayerQuery request, CancellationToken cancellationToken)
    {
        return await _db.Players
            .Where(p => p.Id == request.Id)
            .Select(p => new PlayerDto(p.Id, p.UserName, p.Rating))
            .FirstOrDefaultAsync(cancellationToken);
    }
}