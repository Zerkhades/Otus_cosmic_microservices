using Microsoft.AspNetCore.SignalR;
using System.Text.Json;

namespace AgentGatewayService.Services;

public class AgentHub : Hub
{
    private readonly AgentConnectionManager _connectionManager;
    private readonly ILogger<AgentHub> _logger;

    public AgentHub(AgentConnectionManager connectionManager, ILogger<AgentHub> logger)
    {
        _connectionManager = connectionManager;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var battleId = GetQueryParam<Guid>("battleId");
        var playerId = GetQueryParam<Guid>("playerId");
        
        if (battleId != Guid.Empty && playerId != Guid.Empty)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"battle_{battleId}");
            await Groups.AddToGroupAsync(Context.ConnectionId, $"player_{playerId}");
            
            _logger.LogInformation("Agent connected: ConnectionId={ConnectionId}, BattleId={BattleId}, PlayerId={PlayerId}", 
                Context.ConnectionId, battleId, playerId);
        }
        else
        {
            _logger.LogWarning("Agent connected without proper battle or player ID: ConnectionId={ConnectionId}",
                Context.ConnectionId);
        }
        
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var battleId = GetQueryParam<Guid>("battleId");
        var playerId = GetQueryParam<Guid>("playerId");
        
        if (battleId != Guid.Empty)
        {
            await _connectionManager.UnregisterAgentAsync(battleId, playerId);
            
            _logger.LogInformation("Agent disconnected: ConnectionId={ConnectionId}, BattleId={BattleId}, PlayerId={PlayerId}", 
                Context.ConnectionId, battleId, playerId);
        }
        
        await base.OnDisconnectedAsync(exception);
    }
    
    public async Task SubmitTurn(string turnData)
    {
        try
        {
            var battleId = GetQueryParam<Guid>("battleId");
            var playerId = GetQueryParam<Guid>("playerId");
            
            if (battleId == Guid.Empty || playerId == Guid.Empty)
            {
                _logger.LogWarning("Turn submitted without proper battle or player ID: ConnectionId={ConnectionId}", 
                    Context.ConnectionId);
                return;
            }
            
            var turnInfo = JsonSerializer.Deserialize<TurnInfo>(turnData);
            if (turnInfo == null)
            {
                _logger.LogWarning("Invalid turn data received: {TurnData}", turnData);
                return;
            }
            
            await _connectionManager.SubmitTurnAsync(battleId, playerId, turnInfo.Tick, turnInfo.Payload);
            
            _logger.LogDebug("Turn submitted: BattleId={BattleId}, PlayerId={PlayerId}, Tick={Tick}", 
                battleId, playerId, turnInfo.Tick);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing turn submission: {TurnData}", turnData);
        }
    }
    
    private T GetQueryParam<T>(string key) where T : struct
    {
        var value = Context.GetHttpContext()?.Request.Query[key].FirstOrDefault();
        if (string.IsNullOrEmpty(value))
        {
            return default;
        }
        
        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return default;
        }
    }
}

public class TurnInfo
{
    public int Tick { get; set; }
    public byte[] Payload { get; set; } = Array.Empty<byte>();
}