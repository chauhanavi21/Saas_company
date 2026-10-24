using SaasPlatform.Domain.Common;

namespace SaasPlatform.Domain.Entities;

/// <summary>
/// Long-lived credential used to get new access tokens. Only a hash is stored. Each use rotates it, and
/// presenting an already-used token revokes the whole chain for that user (theft detection).
/// </summary>
public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? ReplacedByTokenHash { get; set; }

    public bool IsActive => RevokedAtUtc is null && ExpiresAtUtc > DateTime.UtcNow;
}
