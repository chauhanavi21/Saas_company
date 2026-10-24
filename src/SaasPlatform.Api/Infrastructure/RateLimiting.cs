using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace SaasPlatform.Api.Infrastructure;

public class RateLimitSettings
{
    public const string Section = "RateLimiting";

    /// <summary>Credential attempts (login, register, refresh, accept invite) allowed per client per window.</summary>
    public int AuthPermitLimit { get; set; } = 10;
    public int AuthWindowSeconds { get; set; } = 60;
    public int GlobalPermitLimit { get; set; } = 600;
}

public static class RateLimiting
{
    /// <summary>Policy for credential endpoints (login, register, refresh, accept invite).</summary>
    public const string Auth = "auth";

    /// <summary>
    /// Built-in ASP.NET Core rate limiting, no external service. Limits are per client IP, using fixed windows.
    /// Counts are in memory, so they reset on restart and aren't shared across several server instances.
    /// Settings are read through IOptions when the first request arrives, so configuration overrides always apply.
    /// </summary>
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<RateLimitSettings>(config.GetSection(RateLimitSettings.Section));

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            o.AddPolicy(Auth, ctx =>
            {
                var s = ctx.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<RateLimitSettings>>().Value;
                return RateLimitPartition.GetFixedWindowLimiter(
                    ClientKey(ctx),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = s.AuthPermitLimit,
                        Window = TimeSpan.FromSeconds(s.AuthWindowSeconds),
                        QueueLimit = 0
                    });
            });

            // A generous ceiling for everything else so a runaway client can't hammer the API.
            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
            {
                var s = ctx.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<RateLimitSettings>>().Value;
                return RateLimitPartition.GetFixedWindowLimiter(
                    "global:" + ClientKey(ctx),
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = s.GlobalPermitLimit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
            });

            o.OnRejected = async (context, ct) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry))
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retry.TotalSeconds).ToString();
                context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new { status = 429, title = "Too many attempts. Please wait a minute and try again." }, ct);
            };
        });
        return services;
    }

    private static string ClientKey(HttpContext ctx) => ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
