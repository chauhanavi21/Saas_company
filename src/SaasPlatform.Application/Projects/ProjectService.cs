using Microsoft.EntityFrameworkCore;
using SaasPlatform.Application.Common;
using SaasPlatform.Domain.Common;
using SaasPlatform.Domain.Entities;

namespace SaasPlatform.Application.Projects;

public record ProjectDto(Guid Id, string Name, string? Description, bool IsArchived, int TaskCount, int DoneCount, DateTime CreatedAtUtc);
public record SaveProjectRequest(string Name, string? Description);

public record TaskDto(
    Guid Id, Guid ProjectId, string Title, string? Description, string Status, string Priority,
    Guid? AssigneeUserId, DateOnly? DueDate, int CommentCount, bool IsOverdue);
public record CreateTaskRequest(string Title, string? Description, Guid? AssigneeUserId, DateOnly? DueDate, string? Priority);
public record UpdateTaskRequest(string Title, string? Description, string Status, Guid? AssigneeUserId, DateOnly? DueDate, string? Priority);

public record CommentDto(Guid Id, Guid AuthorUserId, string AuthorName, string Body, DateTime CreatedAtUtc, bool CanDelete);
public record AddCommentRequest(string Body);

public record TaskQuery(string? Search, string? Status, Guid? AssigneeUserId, bool MineOnly);

public interface IProjectService
{
    Task<List<ProjectDto>> ListAsync(bool includeArchived, string? search, CancellationToken ct);
    Task<ProjectDto> GetAsync(Guid id, CancellationToken ct);
    Task<ProjectDto> CreateAsync(SaveProjectRequest request, CancellationToken ct);
    Task<ProjectDto> UpdateAsync(Guid id, SaveProjectRequest request, CancellationToken ct);
    Task SetArchivedAsync(Guid id, bool archived, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);

    Task<List<TaskDto>> ListTasksAsync(Guid projectId, TaskQuery query, CancellationToken ct);
    Task<TaskDto> CreateTaskAsync(Guid projectId, CreateTaskRequest request, CancellationToken ct);
    Task<TaskDto> UpdateTaskAsync(Guid projectId, Guid taskId, UpdateTaskRequest request, CancellationToken ct);
    Task DeleteTaskAsync(Guid projectId, Guid taskId, CancellationToken ct);

    Task<List<CommentDto>> ListCommentsAsync(Guid projectId, Guid taskId, CancellationToken ct);
    Task<CommentDto> AddCommentAsync(Guid projectId, Guid taskId, string body, CancellationToken ct);
    Task DeleteCommentAsync(Guid projectId, Guid taskId, Guid commentId, CancellationToken ct);
}

/// <summary>Every query goes through the tenant query filter, so a caller can never reach another company's rows.</summary>
public class ProjectService(
    IApplicationDbContext db,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser,
    ICurrentUserName currentName,
    IActivityRecorder activity) : IProjectService
{
    public async Task<List<ProjectDto>> ListAsync(bool includeArchived, string? search, CancellationToken ct)
    {
        var q = db.Projects.Where(p => includeArchived || !p.IsArchived);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(p => EF.Functions.Like(p.Name, $"%{Escape(s)}%", "\\"));
        }
        return await q
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => new ProjectDto(p.Id, p.Name, p.Description, p.IsArchived,
                p.Tasks.Count, p.Tasks.Count(t => t.Status == ProjectTaskStatus.Done), p.CreatedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<ProjectDto> GetAsync(Guid id, CancellationToken ct) =>
        await db.Projects
            .Where(p => p.Id == id)
            .Select(p => new ProjectDto(p.Id, p.Name, p.Description, p.IsArchived,
                p.Tasks.Count, p.Tasks.Count(t => t.Status == ProjectTaskStatus.Done), p.CreatedAtUtc))
            .FirstOrDefaultAsync(ct)
        ?? throw new KeyNotFoundException("Project not found.");

    public async Task<ProjectDto> CreateAsync(SaveProjectRequest request, CancellationToken ct)
    {
        var project = new Project
        {
            TenantId = currentTenant.TenantId ?? throw new InvalidOperationException("No tenant in context."),
            CreatedByUserId = currentUser.UserId ?? throw new InvalidOperationException("No user in context."),
            Name = request.Name.Trim(),
            Description = Clean(request.Description)
        };
        db.Projects.Add(project);
        activity.Record("project.created", "Project", project.Id, project.Name);
        await db.SaveChangesAsync(ct);
        return new ProjectDto(project.Id, project.Name, project.Description, false, 0, 0, project.CreatedAtUtc);
    }

    public async Task<ProjectDto> UpdateAsync(Guid id, SaveProjectRequest request, CancellationToken ct)
    {
        var project = await FindProjectAsync(id, ct);
        project.Name = request.Name.Trim();
        project.Description = Clean(request.Description);
        activity.Record("project.updated", "Project", project.Id, project.Name);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task SetArchivedAsync(Guid id, bool archived, CancellationToken ct)
    {
        var project = await FindProjectAsync(id, ct);
        project.IsArchived = archived;
        activity.Record(archived ? "project.archived" : "project.unarchived", "Project", project.Id, project.Name);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var project = await FindProjectAsync(id, ct);
        activity.Record("project.deleted", "Project", project.Id, project.Name);
        db.Projects.Remove(project); // tasks and their comments cascade
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<TaskDto>> ListTasksAsync(Guid projectId, TaskQuery query, CancellationToken ct)
    {
        await FindProjectAsync(projectId, ct);

        var q = db.Tasks.Where(t => t.ProjectId == projectId);
        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(t => EF.Functions.Like(t.Title, $"%{Escape(query.Search.Trim())}%", "\\"));
        if (Enum.TryParse<ProjectTaskStatus>(query.Status, true, out var status) && !int.TryParse(query.Status, out _))
            q = q.Where(t => t.Status == status);
        if (query.MineOnly && currentUser.UserId is { } me)
            q = q.Where(t => t.AssigneeUserId == me);
        else if (query.AssigneeUserId is { } who)
            q = q.Where(t => t.AssigneeUserId == who);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rows = await q
            .OrderBy(t => t.CreatedAtUtc)
            .Select(t => new
            {
                Task = t,
                Comments = db.TaskComments.Count(c => c.TaskId == t.Id)
            })
            .ToListAsync(ct);
        return rows.Select(r => ToDto(r.Task, r.Comments, today)).ToList();
    }

    public async Task<TaskDto> CreateTaskAsync(Guid projectId, CreateTaskRequest request, CancellationToken ct)
    {
        var project = await FindProjectAsync(projectId, ct);
        if (project.IsArchived) throw new InvalidOperationException("This project is archived.");
        await EnsureAssigneeInTenantAsync(request.AssigneeUserId, ct);

        var task = new ProjectTask
        {
            TenantId = project.TenantId,
            ProjectId = project.Id,
            Title = request.Title.Trim(),
            Description = Clean(request.Description),
            AssigneeUserId = request.AssigneeUserId,
            DueDate = request.DueDate,
            Priority = ParsePriority(request.Priority) ?? TaskPriority.Medium
        };
        db.Tasks.Add(task);
        activity.Record("task.created", "Task", task.Id, $"{task.Title} in {project.Name}");
        await db.SaveChangesAsync(ct);
        return ToDto(task, 0, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    public async Task<TaskDto> UpdateTaskAsync(Guid projectId, Guid taskId, UpdateTaskRequest request, CancellationToken ct)
    {
        var task = await FindTaskAsync(projectId, taskId, ct);
        var status = Enum.Parse<ProjectTaskStatus>(request.Status, ignoreCase: true); // validated by FluentValidation
        await EnsureAssigneeInTenantAsync(request.AssigneeUserId, ct);

        var moved = task.Status != status;
        var reassigned = task.AssigneeUserId != request.AssigneeUserId;

        task.Title = request.Title.Trim();
        task.Description = Clean(request.Description);
        task.Status = status;
        task.AssigneeUserId = request.AssigneeUserId;
        task.DueDate = request.DueDate;
        if (ParsePriority(request.Priority) is { } p) task.Priority = p;

        if (moved) activity.Record("task.moved", "Task", task.Id, $"{task.Title} → {Label(status)}");
        else if (reassigned) activity.Record("task.assigned", "Task", task.Id, task.Title);
        await db.SaveChangesAsync(ct);

        var comments = await db.TaskComments.CountAsync(c => c.TaskId == task.Id, ct);
        return ToDto(task, comments, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    public async Task DeleteTaskAsync(Guid projectId, Guid taskId, CancellationToken ct)
    {
        var task = await FindTaskAsync(projectId, taskId, ct);
        activity.Record("task.deleted", "Task", task.Id, task.Title);
        db.Tasks.Remove(task);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<CommentDto>> ListCommentsAsync(Guid projectId, Guid taskId, CancellationToken ct)
    {
        await FindTaskAsync(projectId, taskId, ct);
        var me = currentUser.UserId;
        var isAdmin = currentUser.Role == Roles.Admin;
        var rows = await db.TaskComments.Where(c => c.TaskId == taskId).OrderBy(c => c.CreatedAtUtc).ToListAsync(ct);
        return rows.Select(c => new CommentDto(c.Id, c.AuthorUserId, c.AuthorName, c.Body, c.CreatedAtUtc, isAdmin || c.AuthorUserId == me)).ToList();
    }

    public async Task<CommentDto> AddCommentAsync(Guid projectId, Guid taskId, string body, CancellationToken ct)
    {
        var task = await FindTaskAsync(projectId, taskId, ct);
        var comment = new TaskComment
        {
            TenantId = task.TenantId,
            TaskId = task.Id,
            AuthorUserId = currentUser.UserId ?? throw new InvalidOperationException("No user in context."),
            AuthorName = currentName.DisplayName ?? "Someone",
            Body = body.Trim()
        };
        db.TaskComments.Add(comment);
        activity.Record("task.commented", "Task", task.Id, task.Title);
        await db.SaveChangesAsync(ct);
        return new CommentDto(comment.Id, comment.AuthorUserId, comment.AuthorName, comment.Body, comment.CreatedAtUtc, true);
    }

    public async Task DeleteCommentAsync(Guid projectId, Guid taskId, Guid commentId, CancellationToken ct)
    {
        await FindTaskAsync(projectId, taskId, ct);
        var comment = await db.TaskComments.FirstOrDefaultAsync(c => c.Id == commentId && c.TaskId == taskId, ct)
            ?? throw new KeyNotFoundException("Comment not found.");

        // Authors can remove their own comments; admins can moderate any.
        if (comment.AuthorUserId != currentUser.UserId && currentUser.Role != Roles.Admin)
            throw new ForbiddenException("You can only delete your own comments.");

        db.TaskComments.Remove(comment);
        await db.SaveChangesAsync(ct);
    }

    private async Task<Project> FindProjectAsync(Guid id, CancellationToken ct) =>
        await db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct)
        ?? throw new KeyNotFoundException("Project not found.");

    private async Task<ProjectTask> FindTaskAsync(Guid projectId, Guid taskId, CancellationToken ct) =>
        await db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId, ct)
        ?? throw new KeyNotFoundException("Task not found.");

    /// <summary>Prevents assigning a task to a user from a different company (the Users filter hides them).</summary>
    private async Task EnsureAssigneeInTenantAsync(Guid? userId, CancellationToken ct)
    {
        if (userId is null) return;
        if (!await db.Users.AnyAsync(u => u.Id == userId, ct))
            throw new InvalidOperationException("Assignee must be a member of your company.");
    }

    private static TaskPriority? ParsePriority(string? v) =>
        Enum.TryParse<TaskPriority>(v, true, out var p) && !int.TryParse(v, out _) ? p : null;

    private static string Label(ProjectTaskStatus s) => s switch
    {
        ProjectTaskStatus.Todo => "To do",
        ProjectTaskStatus.InProgress => "In progress",
        _ => "Done"
    };

    /// <summary>Escapes LIKE wildcards so searching for "50%" matches the literal text.</summary>
    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static TaskDto ToDto(ProjectTask t, int comments, DateOnly today) =>
        new(t.Id, t.ProjectId, t.Title, t.Description, t.Status.ToString(), t.Priority.ToString(),
            t.AssigneeUserId, t.DueDate, comments, t.Status != ProjectTaskStatus.Done && t.DueDate is { } d && d < today);
}
