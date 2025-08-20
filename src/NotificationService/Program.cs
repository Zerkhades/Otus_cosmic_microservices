using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NotificationService.Application.Commands;
using NotificationService.Domains;
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

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
  .AddJwtBearer(o =>
  {
      o.Authority = "http://192.168.9.142:8080/auth";
      o.RequireHttpsMetadata = false;

      o.TokenValidationParameters = new TokenValidationParameters
      {
          ValidateAudience = true,
          ValidAudience = "notification-api",
          NameClaimType = "sub"
      };

      // достаём токен из query для WebSocket
      o.Events = new JwtBearerEvents
      {
          OnMessageReceived = ctx => {
              if (HttpMethods.IsOptions(ctx.Request.Method))
              { // preflight
                  ctx.NoResult(); // не пытаться аутентифицировать
                  return Task.CompletedTask;
              }
              var accessToken = ctx.Request.Query["access_token"];
              if (!string.IsNullOrEmpty(accessToken) &&
                  ctx.HttpContext.Request.Path.StartsWithSegments("/ws/notifications"))
              {
                  ctx.Token = accessToken;
              }
              return Task.CompletedTask;
          }
      };
  });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("notif-scope", p =>
        p.RequireClaim("scope", "notification-api"));

builder.Services.AddSingleton<IUserIdProvider, SubUserIdProvider>();

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
        policy.SetIsOriginAllowed(_ => true)
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

app.UseAuthentication();
app.UseAuthorization();

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