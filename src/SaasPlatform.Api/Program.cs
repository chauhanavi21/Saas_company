using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SaasPlatform.Api.Infrastructure;
using SaasPlatform.Application;
using SaasPlatform.Infrastructure;
using SaasPlatform.Infrastructure.Persistence;
using SaasPlatform.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers(o => o.Filters.Add<ValidationFilter>());
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddApiRateLimiting(builder.Configuration);

// Validation parameters are built from IOptions<JwtSettings> on first use (not at registration time), so the
// signer in JwtTokenService and the validator below always read the same, final configuration.
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => o.MapInboundClaims = false); // keep claim names like "tenant_id" and "sub" as-is
builder.Services.AddOptions<JwtSettings>()
    .Validate(j => !string.IsNullOrWhiteSpace(j.Key) && j.Key.Length >= 32,
        "Jwt:Key must be set (min 32 chars). Use user-secrets or an environment variable.")
    .ValidateOnStart(); // fail at startup, not on the first request
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtSettings>>((o, settings) =>
    {
        var jwt = settings.Value;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            RoleClaimType = "role",
            NameClaimType = "name",
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:4200"];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseApiExceptionHandler();

app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "DENY";
    h["Referrer-Policy"] = "no-referrer";
    h["Cache-Control"] = "no-store"; // API responses carry per-user data
    await next();
});

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

// Apply EF Core migrations on startup for relational stores (tests swap in their own setup).
if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

/// <summary>Exposed so the integration test project can host the app with WebApplicationFactory.</summary>
public partial class Program;
