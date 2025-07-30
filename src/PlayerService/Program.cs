using Confluent.Kafka;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using PlayerService.Application.Commands;
using PlayerService.Application.Queries;
using PlayerService.Infrastructure.Persistence;
using PlayerService.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

// ———————————————————————————
// Configuration
// ———————————————————————————
var cfg = builder.Configuration;

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(cfg.GetConnectionString("Default")));

builder.Services.AddMediatR(cfg => 
    cfg.RegisterServicesFromAssemblyContaining<CreatePlayerCommand>());

builder.Services.AddScoped<IPlayerRepository, PlayerRepository>();
builder.Services.AddScoped<IKafkaProducerWrapper, KafkaProducerWrapper>();

// Kafka producer (example — can be moved to DI extension)
var kafkaConfig = new ProducerConfig { BootstrapServers = cfg["Kafka:BootstrapServers"] };
builder.Services.AddSingleton(_ => new ProducerBuilder<string, string>(kafkaConfig).Build());

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddOpenTelemetry()
    .WithMetrics(b => b
        .AddAspNetCoreInstrumentation()
        .AddPrometheusExporter()
        .AddPrometheusExporter());

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", o =>
    {
        o.Authority = "http://identityserver:7000";
        o.RequireHttpsMetadata = false;
        o.Audience = "player-api";
    });

builder.Services.AddAuthorization();

var app = builder.Build();

//if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseHttpsRedirection();

// ———————————————————————————
// HTTP Endpoints (CQRS)
// ———————————————————————————
app.MapPost("/players", async (CreatePlayerCommand cmd, IMediator mediator) =>
{
    var id = await mediator.Send(cmd);
    return Results.Created($"/players/{id}", new { id });
});

app.MapGet("/players/{id:guid}", async (Guid id, IMediator mediator) =>
{
    var player = await mediator.Send(new GetPlayerQuery(id));
    return player is null ? Results.NotFound() : Results.Ok(player);
});

app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.MapGet("/", () => Results.Content("Player Service is running", contentType: "text/plain"));


app.Run();
