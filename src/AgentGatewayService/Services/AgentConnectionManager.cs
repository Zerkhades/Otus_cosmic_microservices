using AgentGatewayService.Protos;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.AspNetCore.SignalR;

namespace AgentGatewayService.Services;

public class AgentConnectionManager
{
    private readonly ILogger<AgentConnectionManager> _logger;
    private readonly IHubContext<AgentHub> _hubContext;
    private readonly Dictionary<string, AgentConnection> _connections = new();
    private readonly object _lock = new();

    public AgentConnectionManager(ILogger<AgentConnectionManager> logger, IHubContext<AgentHub> hubContext)
    {
        _logger = logger;
        _hubContext = hubContext;
    }

    public async Task<string> RegisterAgentAsync(
        Guid battleId, 
        Guid playerId, 
        BattleSynchronizer.BattleSynchronizerClient battleClient,
        CancellationToken cancellationToken)
    {
        var connectionId = $"{battleId}_{playerId}";
        
        lock (_lock)
        {
            if (_connections.ContainsKey(connectionId))
            {
                _logger.LogWarning("Connection already exists for BattleId={BattleId}, PlayerId={PlayerId}", 
                    battleId, playerId);
                return connectionId;
            }
        }

        try
        {
            // Prepare headers for gRPC call
            var headers = new Metadata
            {
                { "battle-id", battleId.ToString() },
                { "player-id", playerId.ToString() }
            };

            // Create bidirectional streaming call
            var call = battleClient.Connect(headers, cancellationToken: cancellationToken);

            // Create connection object
            var connection = new AgentConnection
            {
                BattleId = battleId,
                PlayerId = playerId,
                Call = call
            };

            // Add to connections dictionary
            lock (_lock)
            {
                _connections[connectionId] = connection;
            }

            // Start reading responses from battle service
            _ = Task.Run(async () =>
            {
                try
                {
                    await foreach (var update in call.ResponseStream.ReadAllAsync(cancellationToken))
                    {
                        await _hubContext.Clients.Group($"player_{playerId}")
                            .SendAsync("ReceiveUpdate", 
                                new { 
                                    battleId = battleId.ToString(),
                                    tick = update.Tick,
                                    state = Convert.ToBase64String(update.State.ToByteArray())
                                }, 
                                cancellationToken);
                    }
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
                {
                    // Normal cancellation, remove connection
                    await UnregisterAgentAsync(battleId, playerId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in battle service communication for BattleId={BattleId}, PlayerId={PlayerId}", 
                        battleId, playerId);
                    await UnregisterAgentAsync(battleId, playerId);
                }
            });

            _logger.LogInformation("Registered new agent connection: BattleId={BattleId}, PlayerId={PlayerId}",
                battleId, playerId);

            return connectionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register agent: BattleId={BattleId}, PlayerId={PlayerId}", 
                battleId, playerId);
            throw;
        }
    }

    public async Task UnregisterAgentAsync(Guid battleId, Guid playerId)
    {
        var connectionId = $"{battleId}_{playerId}";
        
        lock (_lock)
        {
            if (!_connections.TryGetValue(connectionId, out var connection))
            {
                return;
            }

            _connections.Remove(connectionId);
            
            try
            {
                // Cancel the call if it's still active
                connection.CancellationTokenSource?.Cancel();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error cancelling gRPC call for BattleId={BattleId}, PlayerId={PlayerId}", 
                    battleId, playerId);
            }
        }

        await Task.CompletedTask;
    }

    public async Task SubmitTurnAsync(Guid battleId, Guid playerId, int tick, byte[] payload)
    {
        var connectionId = $"{battleId}_{playerId}";
        
        AgentConnection? connection;
        lock (_lock)
        {
            if (!_connections.TryGetValue(connectionId, out connection))
            {
                _logger.LogWarning("No connection found for BattleId={BattleId}, PlayerId={PlayerId}", 
                    battleId, playerId);
                return;
            }
        }

        try
        {
            var turn = new AgentTurn
            {
                PlayerId = playerId.ToString(),
                Tick = tick,
                Payload = ByteString.CopyFrom(payload)
            };

            await connection.Call.RequestStream.WriteAsync(turn);
            
            _logger.LogDebug("Turn sent to battle service: BattleId={BattleId}, PlayerId={PlayerId}, Tick={Tick}", 
                battleId, playerId, tick);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending turn to battle service: BattleId={BattleId}, PlayerId={PlayerId}, Tick={Tick}", 
                battleId, playerId, tick);
            await UnregisterAgentAsync(battleId, playerId);
        }
    }

    private class AgentConnection
    {
        public Guid BattleId { get; init; }
        public Guid PlayerId { get; init; }
        public AsyncDuplexStreamingCall<AgentTurn, ServerUpdate> Call { get; init; } = null!;
        public CancellationTokenSource? CancellationTokenSource { get; set; } = new();
    }
}