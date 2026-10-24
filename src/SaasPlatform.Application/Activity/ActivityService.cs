using Microsoft.EntityFrameworkCore;
using SaasPlatform.Application.Common;

namespace SaasPlatform.Application.Activity;

public record ActivityDto(Guid Id, string ActorName, string Action, string EntityType, Guid? EntityId, string Summary, DateTime AtUtc);

public interface IActivityService
{
    Task<List<ActivityDto>> RecentAsync(int take, CancellationToken ct);
}

public class ActivityService(IApplicationDbContext db) : IActivityService
{
    public async Task<List<ActivityDto>> RecentAsync(int take, CancellationToken ct)
    {
        take = Math.Clamp(take, 1, 100);
        return await db.ActivityLog
            .OrderByDescending(a => a.CreatedAtUtc)
            .Take(take)
            .Select(a => new ActivityDto(a.Id, a.ActorName, a.Action, a.EntityType, a.EntityId, a.Summary, a.CreatedAtUtc))
            .ToListAsync(ct);
    }
}
