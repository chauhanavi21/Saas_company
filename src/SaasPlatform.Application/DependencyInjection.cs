using Microsoft.Extensions.DependencyInjection;
using SaasPlatform.Application.Auth;
using SaasPlatform.Application.Team;

namespace SaasPlatform.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITeamService, TeamService>();
        services.AddScoped<IInvitationService, InvitationService>();
        return services;
    }
}
