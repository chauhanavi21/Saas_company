using Microsoft.EntityFrameworkCore;
using SaasPlatform.Domain.Entities;

namespace SaasPlatform.Application.Common;

public interface IApplicationDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<AppUser> Users { get; }
    DbSet<Invitation> Invitations { get; }
    DbSet<Project> Projects { get; }
    DbSet<ProjectTask> Tasks { get; }
    DbSet<TaskComment> TaskComments { get; }
    DbSet<ActivityEntry> ActivityLog { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>Resolves the tenant of the current request (from the JWT "tenant_id" claim).</summary>
public interface ICurrentTenant
{
    Guid? TenantId { get; }
}

/// <summary>The authenticated user making the current request (from the JWT claims).</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Role { get; }
}

public interface IJwtTokenService
{
    string CreateToken(AppUser user);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

/// <summary>The caller is authenticated but not allowed to do this. Mapped to HTTP 403.</summary>
public class ForbiddenException(string message) : Exception(message);
