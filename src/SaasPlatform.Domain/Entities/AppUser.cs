using SaasPlatform.Domain.Common;

namespace SaasPlatform.Domain.Entities;

public class AppUser : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = Roles.Member;
}
