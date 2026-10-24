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
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectTask> Tasks => Set<ProjectTask>();
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();
    public DbSet<ActivityEntry> ActivityLog => Set<ActivityEntry>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

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

        b.Entity<Invitation>(e =>
        {
            e.HasIndex(i => i.TokenHash).IsUnique();
            e.Property(i => i.Email).HasMaxLength(320);
            e.Property(i => i.Role).HasMaxLength(50);
            e.HasOne<Tenant>().WithMany().HasForeignKey(i => i.TenantId);
            e.HasQueryFilter(i => i.TenantId == currentTenant.TenantId);
        });

        b.Entity<Project>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(200);
            e.Property(p => p.Description).HasMaxLength(2000);
            e.HasIndex(p => p.TenantId);
            e.HasOne<Tenant>().WithMany().HasForeignKey(p => p.TenantId);
            e.HasMany(p => p.Tasks).WithOne().HasForeignKey(t => t.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(p => p.TenantId == currentTenant.TenantId);
        });

        b.Entity<ProjectTask>(e =>
        {
            e.ToTable("Tasks");
            e.Property(t => t.Title).HasMaxLength(300);
            e.Property(t => t.Description).HasMaxLength(4000);
            e.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(t => t.Priority).HasConversion<string>().HasMaxLength(20).HasDefaultValue(TaskPriority.Medium);
            e.HasIndex(t => new { t.ProjectId, t.Status });
            e.HasIndex(t => t.AssigneeUserId);
            e.HasQueryFilter(t => t.TenantId == currentTenant.TenantId);
        });

        b.Entity<TaskComment>(e =>
        {
            e.Property(c => c.Body).HasMaxLength(2000);
            e.Property(c => c.AuthorName).HasMaxLength(200);
            e.HasIndex(c => c.TaskId);
            e.HasOne<ProjectTask>().WithMany().HasForeignKey(c => c.TaskId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(c => c.TenantId == currentTenant.TenantId);
        });

        b.Entity<ActivityEntry>(e =>
        {
            e.Property(a => a.ActorName).HasMaxLength(200);
            e.Property(a => a.Action).HasMaxLength(60);
            e.Property(a => a.EntityType).HasMaxLength(40);
            e.Property(a => a.Summary).HasMaxLength(500);
            e.HasIndex(a => new { a.TenantId, a.CreatedAtUtc });
            e.HasQueryFilter(a => a.TenantId == currentTenant.TenantId);
        });

        // Refresh tokens are looked up by their secret hash before any tenant is known, so they carry no tenant filter.
        b.Entity<RefreshToken>(e =>
        {
            e.HasIndex(r => r.TokenHash).IsUnique();
            e.HasIndex(r => r.UserId);
            e.Property(r => r.TokenHash).HasMaxLength(64);
            e.Property(r => r.ReplacedByTokenHash).HasMaxLength(64);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>().Where(x => x.State == EntityState.Modified))
            entry.Entity.UpdatedAtUtc = DateTime.UtcNow;
        return base.SaveChangesAsync(ct);
    }
}
