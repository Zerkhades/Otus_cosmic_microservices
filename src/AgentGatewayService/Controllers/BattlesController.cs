using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using AgentGatewayService.Services;

namespace AgentGatewayService.Controllers;

[ApiController]
[Route("api/battles")]
public class BattlesController : ControllerBase
{
    private readonly IKafkaProducerWrapper _kafkaProducer;
    private readonly ILogger<BattlesController> _logger;

    public BattlesController(IKafkaProducerWrapper kafkaProducer, ILogger<BattlesController> logger)
    {
        _kafkaProducer = kafkaProducer;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreateBattle([FromBody] CreateBattleRequest request, CancellationToken cancellationToken)
    {
        if (request.TournamentId == Guid.Empty || request.Participants == null || !request.Participants.Any())
        {
            return BadRequest(new { error = "Invalid battle request. TournamentId and at least one participant are required." });
        }

        var battleId = Guid.NewGuid();
        
        // Create event payload
        var eventPayload = new
        {
            battleId = battleId,
            tournamentId = request.TournamentId,
            participants = request.Participants
        };

        // Send event to Kafka
        await _kafkaProducer.PublishAsync("battle.created", JsonSerializer.Serialize(eventPayload), cancellationToken);

        _logger.LogInformation("Created battle request: BattleId={BattleId}, TournamentId={TournamentId}, Participants={ParticipantsCount}", 
            battleId, request.TournamentId, request.Participants.Count);

        return Created($"/api/battles/{battleId}", new { id = battleId });
    }

    [HttpPost("{battleId:guid}/finish")]
    public async Task<IActionResult> FinishBattle(Guid battleId, CancellationToken cancellationToken)
    {
        // Create event payload
        var eventPayload = new
        {
            battleId
        };

        // Send event to Kafka
        await _kafkaProducer.PublishAsync("battle.finish.requested", JsonSerializer.Serialize(eventPayload), cancellationToken);

        _logger.LogInformation("Requested battle finish: BattleId={BattleId}", battleId);

        return Ok(new { message = $"Finish request for battle {battleId} has been sent." });
    }
}

public class CreateBattleRequest
{
    public Guid TournamentId { get; set; }
    public List<Guid> Participants { get; set; } = new();
    public string? RuleSet { get; set; }
}