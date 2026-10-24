using SaasPlatform.Domain.Common;

namespace SaasPlatform.Domain.Entities;

public class Project : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CreatedByUserId { get; set; }
    public bool IsArchived { get; set; }

    public List<ProjectTask> Tasks { get; set; } = [];
}
