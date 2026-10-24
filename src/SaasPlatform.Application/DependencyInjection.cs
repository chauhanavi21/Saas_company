using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SaasPlatform.Application.Account;
using SaasPlatform.Application.Activity;
using SaasPlatform.Application.Auth;
using SaasPlatform.Application.Dashboard;
using SaasPlatform.Application.Projects;
using SaasPlatform.Application.Team;

namespace SaasPlatform.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ITeamService, TeamService>();
        services.AddScoped<IInvitationService, InvitationService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IActivityService, ActivityService>();
        services.AddScoped<IDashboardService, DashboardService>();

        services.AddValidatorsFromAssemblyContaining<IAuthService>(ServiceLifetime.Singleton);
        return services;
    }
}
