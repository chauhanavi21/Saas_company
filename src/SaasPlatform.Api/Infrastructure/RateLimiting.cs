using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace SaasPlatform.Api.Infrastructure;

public static class RateLimiting
{
    /// <summary>Policy for credential endpoints (login, register, refresh, accept invite).</summary>
    public const string Auth = "auth";

    /// <summary>
    /// Built-in ASP.NET Core rate limiting, no external service. Limits are per client IP, using fixed windows.
    /// Counts are in memory, so they reset on restart and aren't shared across several server instances.
    /// </summary>
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration config)
    {
        var permit = config.GetValue("RateLimiting:AuthPermitLimit", 10);
        var windowSeconds = config.GetValue("RateLimiting:AuthWindowSeconds", 60);

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            o.AddPolicy(Auth, ctx => RateLimitPartition.GetFixedWindowLimiter(
                ClientKey(ctx),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permit,
                    Window = TimeSpan.FromSeconds(windowSeconds),
                    QueueLimit = 0
                }));

            // A generous ceiling for everything else so a runaway client can't hammer the API.
            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
                RateLimitPartition.GetFixedWindowLimiter(
                    "global:" + ClientKey(ctx),
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 600, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

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
