using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SaasPlatform.Application.Common;
using SaasPlatform.Infrastructure.Persistence;
using SaasPlatform.Infrastructure.Services;

namespace SaasPlatform.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<JwtSettings>(config.GetSection(JwtSettings.Section));
        services.AddHttpContextAccessor();

        services.AddDbContext<AppDbContext>(o => o.UseSqlite(config.GetConnectionString("Default")));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddScoped<ICurrentTenant, CurrentTenant>();
        services.AddScoped<CurrentUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUser>());
        services.AddScoped<ICurrentUserName>(sp => sp.GetRequiredService<CurrentUser>());
        services.AddScoped<IActivityRecorder, ActivityRecorder>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        return services;
    }
}
