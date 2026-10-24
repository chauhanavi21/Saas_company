using SaasPlatform.Domain.Common;

namespace SaasPlatform.Domain.Entities;

public class TaskComment : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid TaskId { get; set; }
    public Guid AuthorUserId { get; set; }

    /// <summary>Denormalised so comments still show a name after the author is removed from the company.</summary>
    public string AuthorName { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;
}
