using Microsoft.EntityFrameworkCore;
using SaasPlatform.Domain.Entities;

namespace SaasPlatform.Application.Common;

public interface IApplicationDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<AppUser> Users { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>Resolves the tenant of the current request (from the JWT "tenant_id" claim).</summary>
public interface ICurrentTenant
{
    Guid? TenantId { get; }
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
