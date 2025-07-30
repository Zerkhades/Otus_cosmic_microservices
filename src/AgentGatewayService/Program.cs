using AgentGatewayService.Services;
using Confluent.Kafka;
using MediatR;
using OpenTelemetry.Metrics;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

// Add services to the container
builder.Services.AddGrpc();
builder.Services.AddSignalR();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());

// Add gRPC client for BattleService
builder.Services.AddGrpcClient<AgentGatewayService.Protos.BattleSynchronizer.BattleSynchronizerClient>(o =>
{
    o.Address = new Uri(cfg["BattleService:Grpc"] ?? "https://battle-service:5003");
});

// Kafka producer for sending events
var kafkaConfig = new ProducerConfig { BootstrapServers = cfg["Kafka:BootstrapServers"] };
builder.Services.AddSingleton(_ => new ProducerBuilder<string, string>(kafkaConfig).Build());
builder.Services.AddSingleton<IKafkaProducerWrapper, KafkaProducerWrapper>();

// Add controllers and API explorer
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add connection manager for agent connections
builder.Services.AddSingleton<AgentConnectionManager>();

// CORS configuration for web clients
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowedOrigins", policy =>
    {
        policy.WithOrigins(cfg.GetSection("AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:5173" })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddOpenTelemetry()
    .WithMetrics(b =>
    {
        b.AddAspNetCoreInstrumentation();
        b.AddRuntimeInstrumentation();
        b.AddProcessInstrumentation();
        b.AddMeter("AgentService");
        b.AddPrometheusExporter();
    });



var app = builder.Build();

// Configure the HTTP request pipeline
//if (app.Environment.IsDevelopment())
//{
app.UseSwagger();
app.UseSwaggerUI();
//}

app.UseCors("AllowedOrigins");
app.UseRouting();

// Map SignalR hubs
app.MapHub<AgentHub>("/hubs/agent");

// Map controllers
app.MapControllers();

// Map minimal API endpoints
app.MapPost("/api/battles/{battleId:guid}/connect", async (
    Guid battleId,
    Guid playerId,
    AgentConnectionManager connectionManager,
    AgentGatewayService.Protos.BattleSynchronizer.BattleSynchronizerClient battleClient,
    CancellationToken cancellationToken) =>
{
    var connectionId = await connectionManager.RegisterAgentAsync(battleId, playerId, battleClient, cancellationToken);
    return Results.Ok(new { connectionId });
});

app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.MapGet("/", () => Results.Content("Agent Gateway Service up", contentType: "text/plain"));

app.Run();