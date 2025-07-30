using MediatR;
using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Commands;
using NotificationService.Infrastructure.Hubs;
using NotificationService.Infrastructure.Kafka;
using NotificationService.Infrastructure.Persistence;
using NotificationService.Infrastructure.Repositories;
using OpenTelemetry.Metrics;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

// EFCore Postgres

var cnstr = cfg.GetConnectionString("Default");
builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseNpgsql(cfg.GetConnectionString("Default")));

// SignalR
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// MediatR
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<EnqueueNotificationCommand>());

// Repository
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();

// Kafka consumer background service
builder.Services.AddHostedService<KafkaConsumerHostedService>();

// API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS for web clients (important for SignalR)
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
    .WithMetrics(b => b
        .AddRuntimeInstrumentation()
        .AddProcessInstrumentation()
        .AddAspNetCoreInstrumentation()
        .AddMeter("NotificationService")
        .AddPrometheusExporter());


var app = builder.Build();

// Configure the HTTP request pipeline
//if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    //db.Database.EnsureCreated();
    db.Database.Migrate();
}


app.UseCors("AllowedOrigins");

// SignalR endpoint
app.MapHub<NotificationHub>("/ws/notifications");

// HTTP endpoints
app.MapGet("/api/notifications/unread/{userId:guid}", async (Guid userId, IMediator mediator) =>
{
    var notifications = await mediator.Send(new NotificationService.Application.Queries.GetUnreadQuery(userId));
    return Results.Ok(notifications);
});

app.MapPost("/api/notifications", async (EnqueueNotificationCommand command, IMediator mediator) =>
{
    var id = await mediator.Send(command);
    return Results.Created($"/api/notifications/{id}", new { id });
});

app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.MapGet("/", () => Results.Content("Notification Service is running", contentType: "text/plain"));

app.Run();