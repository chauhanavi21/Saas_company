namespace SaasPlatform.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}

/// <summary>Marks an entity as owned by a tenant. Rows are filtered by TenantId automatically.</summary>
public interface ITenantScoped
{
    Guid TenantId { get; set; }
}
