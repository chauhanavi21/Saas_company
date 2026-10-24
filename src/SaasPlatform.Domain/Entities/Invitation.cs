using SaasPlatform.Domain.Common;

namespace SaasPlatform.Domain.Entities;

public class Invitation : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = Roles.Member;

    /// <summary>SHA-256 hash of the invite token. The raw token is shown once and never stored.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? AcceptedAtUtc { get; set; }
    public Guid InvitedByUserId { get; set; }

    public bool IsPending => AcceptedAtUtc is null && ExpiresAtUtc > DateTime.UtcNow;
}
