using Confluent.Kafka;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using PlayerService.Application.Commands;
using PlayerService.Application.Queries;
using PlayerService.Infrastructure.Persistence;
using PlayerService.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

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

// Auth: accept tokens from internal and external issuers
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, o =>
    {
        o.RequireHttpsMetadata = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false,
            ValidIssuers = new[]
            {
                "http://identityserver:7000/auth",
                "http://192.168.9.142:8080/auth",
                "http://localhost:8080/auth"
            }
        };
        // If you call IdentityServer directly inside cluster
        o.Authority = "http://identityserver:7000/auth";
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
app.UseAuthentication();
app.UseAuthorization();

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
