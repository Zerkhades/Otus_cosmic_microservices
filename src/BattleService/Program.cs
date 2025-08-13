using BattleService.Application.Commands;
using BattleService.Application.Events;
using BattleService.Application.Worlds;
using BattleService.GameLogic;
using BattleService.GameLogic.Engine;
using BattleService.Infrastructure.Kafka;
using BattleService.Infrastructure.Services;
using BattleService.Infrastructure.State;
using BattleService.Services;
using Confluent.Kafka;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(o =>
{
    // HTTP/1.1 для REST/health/metrics
    o.ListenAnyIP(5003, lo => lo.Protocols = HttpProtocols.Http1);

    // HTTP/2 (h2c) только для gRPC
    o.ListenAnyIP(5007, lo => lo.Protocols = HttpProtocols.Http2);
});

var cfg = builder.Configuration;

// gRPC
builder.Services.AddGrpc();

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
                "http://identityserver:7000/auth",
                "http://localhost:8080/auth"
            }
        };
    });

builder.Services.AddAuthorization();

// MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<FinishBattleCommand>());

// In-memory battle store (replace with Redis later)
builder.Services.AddSingleton<IBattleStore, InMemoryBattleStore>();

// Battle timer service
builder.Services.AddHostedService<BattleTimerService>();

// Kafka producer
var kafkaConfig = new ProducerConfig { BootstrapServers = cfg["Kafka:BootstrapServers"] };
builder.Services.AddSingleton(_ => new ProducerBuilder<string, string>(kafkaConfig).Build());
builder.Services.AddSingleton<IKafkaProducerWrapper, KafkaProducerWrapper>();

// Kafka consumer background service
builder.Services.AddHostedService<KafkaConsumerHostedService>();

builder.Services.AddGameLogic();
builder.Services.AddSingleton<IGameLoopFactory, GameLoopFactory>();
builder.Services.AddSingleton<BattleWorldManager>();
builder.Services.AddHostedService<WorldCleanupService>();

builder.Services.AddOpenTelemetry()
    .WithMetrics(b =>
    {
        b.AddAspNetCoreInstrumentation();
        b.AddRuntimeInstrumentation();
        b.AddProcessInstrumentation();
        b.AddMeter("BattleService");

        //  ==== Prometheus =====
        b.AddPrometheusExporter();
    });

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Add gRPC service
app.MapGrpcService<BattleSyncService>();

// Add minimal API endpoint to manually finish a battle
app.MapPost("/api/battles/{id:guid}/finish", async (Guid id, IMediator mediator) =>
{
    var result = await mediator.Send(new FinishBattleCommand(id));
    return result ? Results.Ok() : Results.NotFound();
});

app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.MapGet("/", () => Results.Content("Battle Service up", contentType: "text/plain"));

app.MapGet("/healthz", () => Results.Ok("ok"));

app.Run();