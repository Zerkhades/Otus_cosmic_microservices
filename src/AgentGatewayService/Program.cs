using AgentGatewayService.Controllers;
using AgentGatewayService.Protos;
using AgentGatewayService.Services;
using Confluent.Kafka;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using static System.Net.WebRequestMethods;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

// Разрешаем HTTP/2 без TLS для gRPC (h2c)
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

// --- Services ---

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddGrpc();
builder.Services.AddSignalR();

builder.Services.AddMediatR(opt => opt.RegisterServicesFromAssemblyContaining<Program>());

// gRPC client
builder.Services
  .AddGrpcClient<BattleSynchronizer.BattleSynchronizerClient>(o =>
  {
      o.Address = new Uri(cfg["BattleService:Grpc"] ?? "http://battle-service:5007");
  })
  .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
  {
      EnableMultipleHttp2Connections = true
  });

// Kafka
var kafkaConfig = new ProducerConfig { BootstrapServers = cfg["Kafka:BootstrapServers"] ?? "kafka:9092" };
builder.Services.AddSingleton(_ => new ProducerBuilder<string, string>(kafkaConfig).Build());
builder.Services.AddSingleton<IKafkaProducerWrapper, KafkaProducerWrapper>();

// Менеджер подключений
builder.Services.AddSingleton<AgentConnectionManager>();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowedOrigins", policy =>
    {
        // Разрешаем локальные и LAN-оригины по умолчанию, если не задано в конфиге
        var allowed = cfg.GetSection("AllowedOrigins").Get<string[]>()
                     ?? new[]
                     {
                         "http://localhost:5173",
                         "http://192.168.9.142:5173"
                     };
        policy.WithOrigins(allowed)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// OpenTelemetry (метрики + /metrics)
builder.Services.AddOpenTelemetry()
    .WithMetrics(b =>
    {
        b.AddAspNetCoreInstrumentation();
        b.AddRuntimeInstrumentation();
        b.AddProcessInstrumentation();
        b.AddMeter("AgentService");
        b.AddPrometheusExporter();
    });

// AuthN/AuthZ
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.Authority = "http://identityserver:7000/auth";
        o.RequireHttpsMetadata = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false,
            ValidIssuers = new[]
            {
                "http://identityserver:7000/auth", // внутренняя
                "http://192.168.9.142:8080/auth",
                "http://localhost:8080/auth"       // внешняя (через YARP)
            }
        };
        // Поднимаем токен из query-параметра для /ws/game
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                if (ctx.Request.Path.StartsWithSegments("/ws/game"))
                {
                    var t = ctx.Request.Query["access_token"];
                    if (!string.IsNullOrEmpty(t))
                        ctx.Token = t;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// --- Pipeline порядок важен ---

app.UseSwagger();
app.UseSwaggerUI();

app.UseRouting();
app.UseCors("AllowedOrigins");
app.UseAuthentication();
app.UseAuthorization();
app.UseWebSockets();

// Метрики Prometheus
app.UseOpenTelemetryPrometheusScrapingEndpoint();

// --- Endpoints ---

// SignalR
app.MapHub<AgentHub>("/hubs/agent");

// Контроллеры (в т.ч. [Authorize] на экшенах)
app.MapControllers();

// Health
app.MapGet("/healthz", () => Results.Ok("ok"));

// Статус
app.MapGet("/", () => Results.Content("Agent Gateway Service up", contentType: "text/plain"));

// матчмейкинг — требует авторизации
app.MapPost("/api/matchmaking/casual", async (
    ClaimsPrincipal user,
    HttpContext ctx,
    IKafkaProducerWrapper kafka,
    CancellationToken ct) =>
{
    var tournamentId = "42aa8186-e520-49a3-9631-847d8b84129b";
    var battleId = "42aa8186-e520-49a3-9631-847d8b84129a";

    var payload = JsonSerializer.Serialize(new
    {
        battleId,
        tournamentId,
        participants = new[] { "42aa8186-e520-49a3-9631-847d8b84129f" , "3bcd880d-3e63-4787-bc10-72ef4326b4a5" }
    });

    await kafka.PublishAsync("battle.created", payload, ct);

    return Results.Ok(new { battleId });
});//.RequireAuthorization();

// Подключение агента через менеджер
app.MapPost("/api/battles/{battleId:guid}/connect", async (
    Guid battleId,
    [FromQuery] Guid playerId,
    AgentConnectionManager connectionManager,
    BattleSynchronizer.BattleSynchronizerClient battleClient,
    CancellationToken cancellationToken) =>
{
    var connectionId = await connectionManager.RegisterAgentAsync(battleId, playerId, battleClient, cancellationToken);
    return Results.Ok(new { connectionId });
});

// WebSocket → gRPC мост (основной поток игры)
app.Map("/ws/game", async (
    HttpContext ctx,
    AgentGatewayService.Protos.BattleSynchronizer.BattleSynchronizerClient battleClient,
    ILoggerFactory loggerFactory) =>
{
    var logger = loggerFactory.CreateLogger("WsGame");

    // 1) Проверка аутентификации
    if (!ctx.User.Identity?.IsAuthenticated ?? true)
    {
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await ctx.Response.WriteAsync("unauthorized");
        return;
    }

    // 2) Извлечём playerId из токена
    var playerId = ctx.User.FindFirst("sub")?.Value
                   ?? ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    if (string.IsNullOrWhiteSpace(playerId))
    {
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await ctx.Response.WriteAsync("missing sub");
        return;
    }

    // 3) battleId из query
    if (!Guid.TryParse(ctx.Request.Query["battleId"], out var battleId))
    {
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        await ctx.Response.WriteAsync("battleId required");
        return;
    }

    // 4) Поднимаем токен из query для проброса в gRPC (если нужен на бэкенде)
    var token = ctx.Request.Query["access_token"].ToString();

    if (!ctx.WebSockets.IsWebSocketRequest)
    {
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        await ctx.Response.WriteAsync("expected WebSocket");
        return;
    }

    using var ws = await ctx.WebSockets.AcceptWebSocketAsync();

    // 5) Метаданные к gRPC-вызову
    var headers = new Grpc.Core.Metadata
    {
        { "battle-id", battleId.ToString() }
    };
    if (!string.IsNullOrEmpty(token))
        headers.Add("Authorization", $"Bearer {token}");

    // 6) Открываем дуплексный стрим
    Grpc.Core.AsyncDuplexStreamingCall<AgentGatewayService.Protos.AgentTurn, AgentGatewayService.Protos.ServerUpdate>? call;
    try
    {
        call = battleClient.Connect(headers: headers, cancellationToken: ctx.RequestAborted);
        logger.LogInformation("gRPC Connect opened for battle {BattleId}, player {PlayerId}", battleId, playerId);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to open gRPC Connect");
        await ws.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.InternalServerError, "grpc connect failed", ctx.RequestAborted);
        return;
    }

    // 7) WS → gRPC
    var tSend = Task.Run(async () =>
    {
        var buffer = new byte[32 * 1024];

        try
        {
            while (!ctx.RequestAborted.IsCancellationRequested)
            {
                var res = await ws.ReceiveAsync(buffer, ctx.RequestAborted);
                if (res.MessageType == System.Net.WebSockets.WebSocketMessageType.Close)
                    break;

                var payload = Google.Protobuf.ByteString.CopyFrom(buffer.AsSpan(0, res.Count).ToArray());

                await call.RequestStream.WriteAsync(new AgentGatewayService.Protos.AgentTurn
                {
                    PlayerId = playerId,
                    Tick = (int)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Payload = payload
                }, ctx.RequestAborted);
            }

            await call.RequestStream.CompleteAsync();
        }
        catch (OperationCanceledException)
        {
            // ignore – shutting down
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "WS → gRPC loop ended");
            try { await call.RequestStream.CompleteAsync(); } catch { /* ignore */ }
        }
    }, ctx.RequestAborted);

    // 8) gRPC → WS
    var tRecv = Task.Run(async () =>
    {
        try
        {
            await foreach (var update in call.ResponseStream.ReadAllAsync(ctx.RequestAborted))
            {
                var stateBytes = update.State.ToByteArray();

                using var ms = new MemoryStream();
                using (var writer = new Utf8JsonWriter(ms))
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("tick", update.Tick);
                    writer.WritePropertyName("state");
                    using var doc = JsonDocument.Parse(stateBytes);
                    doc.RootElement.WriteTo(writer);
                    writer.WriteEndObject();
                }
                await ws.SendAsync(
                    ms.ToArray(),
                    System.Net.WebSockets.WebSocketMessageType.Text,
                    true,
                    ctx.RequestAborted);
            }
        }
        catch (OperationCanceledException)
        {
            // ignore – shutting down
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "gRPC → WS loop ended");
        }
    }, ctx.RequestAborted);

    // 9) Ждём завершения одного из направлений и закрываем WS
    await Task.WhenAny(tSend, tRecv);

    try
    {
        await ws.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "bye", ctx.RequestAborted);
    }
    catch { /* ignore */ }
}).RequireAuthorization();

app.Run();
