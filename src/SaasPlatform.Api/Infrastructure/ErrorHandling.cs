using Microsoft.AspNetCore.Diagnostics;
using SaasPlatform.Application.Common;

namespace SaasPlatform.Api.Infrastructure;

public static class ErrorHandling
{
    /// <summary>Maps domain exceptions to HTTP statuses. Unexpected errors are logged and returned without internals.</summary>
    public static void UseApiExceptionHandler(this WebApplication app) =>
        app.UseExceptionHandler(errorApp => errorApp.Run(async ctx =>
        {
            var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
            var (status, title) = ex switch
            {
                InvalidOperationException => (StatusCodes.Status400BadRequest, ex.Message),
                KeyNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
                ForbiddenException => (StatusCodes.Status403Forbidden, ex.Message),
                BadHttpRequestException => (StatusCodes.Status400BadRequest, "The request body is invalid."),
                _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
            };

            if (status == StatusCodes.Status500InternalServerError)
                ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Unhandled").LogError(ex, "Unhandled exception");

            ctx.Response.StatusCode = status;
            await ctx.Response.WriteAsJsonAsync(new { status, title });
        }));
}
