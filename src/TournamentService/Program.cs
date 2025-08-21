using Confluent.Kafka;
using MediatR;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using TournamentService.Application.Commands;
using TournamentService.Application.Queries;
using TournamentService.Infrastructure.Persistence;
using TournamentService.Infrastructure.Repositories;
using TournamentService.Infrastructure.Repositories.Kafka;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

// Mongo
builder.Services.Configure<MongoSettings>(cfg.GetSection("Mongo"));
builder.Services.AddSingleton<MongoContext>();

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

// Auth (if endpoints require auth later) – configure issuers now
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.RequireHttpsMetadata = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false,
            ValidIssuers = new[]
            {
                "http://identityserver:7000/auth",
                "http://192.168.9.142:8080/auth",
                "http://localhost:5173",
                "http://localhost:8080/auth"
            }
        };
        o.Authority = "http://identityserver:7000/auth";
    });

builder.Services.AddAuthorization();

//builder.Services.AddCors(options =>
//{
//    options.AddPolicy("AllowedOrigins", policy =>
//    {
//        policy.SetIsOriginAllowed(_ => true)
//              .AllowAnyHeader()
//              .AllowAnyMethod()
//              .AllowCredentials();
//    });
//});

var app = builder.Build();

//if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

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

app.MapGet("/", () => Results.Content("Tournament Service up", contentType: "text/plain"));

app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.Run();