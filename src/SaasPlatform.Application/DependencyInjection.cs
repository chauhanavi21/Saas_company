using Microsoft.Extensions.DependencyInjection;
using SaasPlatform.Application.Auth;

namespace SaasPlatform.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}
