using Confluent.Kafka;
using MediatR;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using TournamentService.Application.Commands;
using TournamentService.Application.Queries;
using TournamentService.Infrastructure.Persistence;
using TournamentService.Infrastructure.Repositories;
using TournamentService.Infrastructure.Repositories.Kafka;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

// Mongo
builder.Services.Configure<MongoSettings>(cfg.GetSection("Mongo"));
builder.Services.AddSingleton<MongoContext>();

// MediatR
builder.Services.AddMediatR(cfg => 
    cfg.RegisterServicesFromAssemblyContaining<CreateTournamentCommand>());

// Repository
builder.Services.AddScoped<ITournamentRepository, TournamentRepository>();

// Kafka Producer
builder.Services.AddScoped<IKafkaProducerWrapper, KafkaProducerWrapper>();
var kafkaConfig = new ProducerConfig { BootstrapServers = cfg["Kafka:BootstrapServers"] };
builder.Services.AddSingleton(_ => new ProducerBuilder<string, string>(kafkaConfig).Build());

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddOpenTelemetry()
    .WithMetrics(b => b
        .AddAspNetCoreInstrumentation()
        .AddMeter("TournamentService")
        .AddPrometheusExporter());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// ————————————————————————
// HTTP Endpoints
// ————————————————————————
app.MapPost("/tournaments", async (CreateTournamentCommand cmd, IMediator mediator) =>
{
    var id = await mediator.Send(cmd);
    return Results.Created($"/tournaments/{id}", new { id });
});

app.MapPost("/tournaments/{id:guid}/register", async (Guid id, RegisterPlayerCommand.Body body, IMediator mediator) =>
{
    var result = await mediator.Send(new RegisterPlayerCommand(id, body.PlayerId));
    return result ? Results.Ok() : Results.BadRequest("Registration failed");
});

app.MapGet("/tournaments/upcoming", async (IMediator mediator) =>
{
    var list = await mediator.Send(new GetUpcomingTournamentsQuery());
    return Results.Ok(list);
});

app.MapGet("/tournaments/{id:guid}", async (Guid id, IMediator mediator) =>
{
    var dto = await mediator.Send(new GetTournamentDetailsQuery(id));
    return dto is null ? Results.NotFound() : Results.Ok(dto);
});

app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.Run();