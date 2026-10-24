using SaasPlatform.Domain.Entities;

namespace SaasPlatform.Application.Common;

public interface IActivityRecorder
{
    /// <summary>Queues an activity entry on the current unit of work. It is saved with the caller's next SaveChanges.</summary>
    void Record(string action, string entityType, Guid? entityId, string summary);
}

public class ActivityRecorder(IApplicationDbContext db, ICurrentTenant tenant, ICurrentUser user, ICurrentUserName names)
    : IActivityRecorder
{
    public void Record(string action, string entityType, Guid? entityId, string summary)
    {
        if (tenant.TenantId is not { } tenantId) return;
        db.ActivityLog.Add(new ActivityEntry
        {
            TenantId = tenantId,
            ActorUserId = user.UserId,
            ActorName = names.DisplayName ?? "Someone",
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Summary = summary.Length > 500 ? summary[..500] : summary
        });
    }
}

/// <summary>The display name of the signed-in user (from the JWT "name" claim).</summary>
public interface ICurrentUserName
{
    string? DisplayName { get; }
}
