using Microsoft.EntityFrameworkCore;
using SaasPlatform.Application.Common;
using SaasPlatform.Domain.Common;
using SaasPlatform.Domain.Entities;

namespace SaasPlatform.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentTenant currentTenant)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<AppUser> Users => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Tenant>(e =>
        {
            e.HasIndex(t => t.Slug).IsUnique();
            e.Property(t => t.Name).HasMaxLength(200);
        });

        b.Entity<AppUser>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Email).HasMaxLength(320);
            e.HasOne<Tenant>().WithMany().HasForeignKey(u => u.TenantId);
            // Row-level tenant isolation: every query is scoped to the caller's tenant.
            e.HasQueryFilter(u => u.TenantId == currentTenant.TenantId);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>().Where(x => x.State == EntityState.Modified))
            entry.Entity.UpdatedAtUtc = DateTime.UtcNow;
        return base.SaveChangesAsync(ct);
    }
}
