using Microsoft.EntityFrameworkCore;
using SaasPlatform.Application.Common;
using SaasPlatform.Domain.Entities;

namespace SaasPlatform.Application.Dashboard;

public record StatusCount(string Status, int Count);
public record MyTaskDto(Guid Id, Guid ProjectId, string ProjectName, string Title, string Status, string Priority, DateOnly? DueDate, bool IsOverdue);
public record DashboardDto(
    int ActiveProjects,
    int TeamMembers,
    int OpenTasks,
    int OverdueTasks,
    int CompletedThisWeek,
    List<StatusCount> TasksByStatus,
    List<MyTaskDto> MyTasks);

public interface IDashboardService
{
    Task<DashboardDto> GetAsync(CancellationToken ct);
}

public class DashboardService(IApplicationDbContext db, ICurrentUser currentUser) : IDashboardService
{
    public async Task<DashboardDto> GetAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var weekAgo = DateTime.UtcNow.AddDays(-7);
        var me = currentUser.UserId;

        // Archived projects drop out of every number so the dashboard reflects live work.
        var live = db.Tasks.Where(t => db.Projects.Any(p => p.Id == t.ProjectId && !p.IsArchived));

        var byStatus = await live.GroupBy(t => t.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        int Count(ProjectTaskStatus s) => byStatus.FirstOrDefault(x => x.Key == s)?.Count ?? 0;

        var overdue = await live.CountAsync(t => t.Status != ProjectTaskStatus.Done && t.DueDate != null && t.DueDate < today, ct);
        var doneThisWeek = await live.CountAsync(t => t.Status == ProjectTaskStatus.Done && t.UpdatedAtUtc != null && t.UpdatedAtUtc >= weekAgo, ct);

        var mine = await live
            .Where(t => t.AssigneeUserId == me && t.Status != ProjectTaskStatus.Done)
            .OrderBy(t => t.DueDate == null).ThenBy(t => t.DueDate).ThenByDescending(t => t.Priority)
            .Take(8)
            .Select(t => new
            {
                t.Id, t.ProjectId, t.Title, t.Status, t.Priority, t.DueDate,
                ProjectName = db.Projects.Where(p => p.Id == t.ProjectId).Select(p => p.Name).First()
            })
            .ToListAsync(ct);

        return new DashboardDto(
            ActiveProjects: await db.Projects.CountAsync(p => !p.IsArchived, ct),
            TeamMembers: await db.Users.CountAsync(ct),
            OpenTasks: Count(ProjectTaskStatus.Todo) + Count(ProjectTaskStatus.InProgress),
            OverdueTasks: overdue,
            CompletedThisWeek: doneThisWeek,
            TasksByStatus:
            [
                new("Todo", Count(ProjectTaskStatus.Todo)),
                new("InProgress", Count(ProjectTaskStatus.InProgress)),
                new("Done", Count(ProjectTaskStatus.Done))
            ],
            MyTasks: mine.Select(t => new MyTaskDto(t.Id, t.ProjectId, t.ProjectName, t.Title, t.Status.ToString(),
                t.Priority.ToString(), t.DueDate, t.DueDate is { } d && d < today)).ToList());
    }
}
