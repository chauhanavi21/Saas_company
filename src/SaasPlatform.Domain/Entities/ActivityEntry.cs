using SaasPlatform.Domain.Common;

namespace SaasPlatform.Domain.Entities;

/// <summary>Append-only audit trail shown as the company's activity feed.</summary>
public class ActivityEntry : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid? ActorUserId { get; set; }
    public string ActorName { get; set; } = string.Empty;

    /// <summary>Machine-readable verb, e.g. "project.created" or "task.moved".</summary>
    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }

    /// <summary>Human-readable sentence fragment, e.g. "Website Redesign".</summary>
    public string Summary { get; set; } = string.Empty;
}
