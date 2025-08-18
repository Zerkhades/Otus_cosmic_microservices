using IdentityServer;
using IdentityServer.Data;
using IdentityServer.Models;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<AuthDb>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = ".AspNetCore.Identity.Application";
    o.Cookie.SameSite = SameSiteMode.Lax;        // ← ключевое
    o.Cookie.SecurePolicy = CookieSecurePolicy.None; // dev: http
});

builder.Services.ConfigureExternalCookie(o =>
{
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.SecurePolicy = CookieSecurePolicy.None;
});

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<AuthDb>()
    .AddDefaultTokenProviders()
    .AddDefaultUI();

builder.Services.AddIdentityServer(options =>
{
    options.IssuerUri = "http://192.168.9.142:8080/auth";
    options.Authentication.CookieSameSiteMode = SameSiteMode.Lax;
    options.Authentication.CheckSessionCookieSameSiteMode = SameSiteMode.Lax;
})
    .AddInMemoryIdentityResources(Config.IdentityResources)
    .AddInMemoryApiScopes(Config.ApiScopes)
    .AddInMemoryApiResources(Config.ApiResources)
    .AddInMemoryClients(Config.Clients)
    .AddAspNetIdentity<ApplicationUser>();

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});


builder.Services.AddCors(opt =>
{
    opt.AddPolicy("spa", p => p
        .WithOrigins("http://localhost:5173",
                     "http://192.168.9.142:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()); // на будущее для эндпоинтов, где нужны куки
});

builder.Services.AddRazorPages();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuthDb>();
    db.Database.Migrate();
}

app.MapGet("/", () => "Identity server up");

await Seed.CreateTestUser(app.Services);

{
    app.MapOpenApi();
}


// ПОРЯДОК ВАЖЕН:
app.UseForwardedHeaders();     // <— до PathBase/IdentityServer
app.UsePathBase("/auth");

// CORS для SPA-страниц IS (не обязателен для токен-эндпоинтов)
app.UseCors("spa");

app.UseCookiePolicy(new CookiePolicyOptions
{
    MinimumSameSitePolicy = SameSiteMode.Lax,
    Secure = CookieSecurePolicy.None
});

// dev: не редиректим в https за прокси
//app.UseHttpsRedirection();

app.UseAuthentication();

app.UseIdentityServer();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

app.Run();
