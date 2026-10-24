using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SaasPlatform.Application.Common;

namespace SaasPlatform.Infrastructure.Persistence;

/// <summary>Used only by `dotnet ef` so tooling doesn't need to boot the whole web host (and its secrets).</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options;
        return new AppDbContext(options, new NoTenant());
    }

    private sealed class NoTenant : ICurrentTenant
    {
        public Guid? TenantId => null;
    }
}
