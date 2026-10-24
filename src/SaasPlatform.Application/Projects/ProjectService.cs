using Microsoft.EntityFrameworkCore;
using SaasPlatform.Application.Common;
using SaasPlatform.Domain.Entities;

namespace SaasPlatform.Application.Projects;

public record ProjectDto(Guid Id, string Name, string? Description, bool IsArchived, int TaskCount, int DoneCount, DateTime CreatedAtUtc);
public record SaveProjectRequest(string Name, string? Description);

public record TaskDto(Guid Id, Guid ProjectId, string Title, string? Description, string Status, Guid? AssigneeUserId, DateOnly? DueDate);
public record CreateTaskRequest(string Title, string? Description, Guid? AssigneeUserId, DateOnly? DueDate);
public record UpdateTaskRequest(string Title, string? Description, string Status, Guid? AssigneeUserId, DateOnly? DueDate);

public interface IProjectService
{
    Task<List<ProjectDto>> ListAsync(bool includeArchived, CancellationToken ct);
    Task<ProjectDto> GetAsync(Guid id, CancellationToken ct);
    Task<ProjectDto> CreateAsync(SaveProjectRequest request, CancellationToken ct);
    Task<ProjectDto> UpdateAsync(Guid id, SaveProjectRequest request, CancellationToken ct);
    Task SetArchivedAsync(Guid id, bool archived, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);

    Task<List<TaskDto>> ListTasksAsync(Guid projectId, CancellationToken ct);
    Task<TaskDto> CreateTaskAsync(Guid projectId, CreateTaskRequest request, CancellationToken ct);
    Task<TaskDto> UpdateTaskAsync(Guid projectId, Guid taskId, UpdateTaskRequest request, CancellationToken ct);
    Task DeleteTaskAsync(Guid projectId, Guid taskId, CancellationToken ct);
}

/// <summary>Every query goes through the tenant query filter, so a caller can never reach another company's rows.</summary>
public class ProjectService(IApplicationDbContext db, ICurrentTenant currentTenant, ICurrentUser currentUser) : IProjectService
{
    public async Task<List<ProjectDto>> ListAsync(bool includeArchived, CancellationToken ct) =>
        await db.Projects
            .Where(p => includeArchived || !p.IsArchived)
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => new ProjectDto(p.Id, p.Name, p.Description, p.IsArchived,
                p.Tasks.Count, p.Tasks.Count(t => t.Status == ProjectTaskStatus.Done), p.CreatedAtUtc))
            .ToListAsync(ct);

    public async Task<ProjectDto> GetAsync(Guid id, CancellationToken ct) =>
        await db.Projects
            .Where(p => p.Id == id)
            .Select(p => new ProjectDto(p.Id, p.Name, p.Description, p.IsArchived,
                p.Tasks.Count, p.Tasks.Count(t => t.Status == ProjectTaskStatus.Done), p.CreatedAtUtc))
            .FirstOrDefaultAsync(ct)
        ?? throw new KeyNotFoundException("Project not found.");

    public async Task<ProjectDto> CreateAsync(SaveProjectRequest request, CancellationToken ct)
    {
        var name = ValidateName(request.Name);
        var project = new Project
        {
            TenantId = currentTenant.TenantId ?? throw new InvalidOperationException("No tenant in context."),
            CreatedByUserId = currentUser.UserId ?? throw new InvalidOperationException("No user in context."),
            Name = name,
            Description = Clean(request.Description)
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);
        return new ProjectDto(project.Id, project.Name, project.Description, false, 0, 0, project.CreatedAtUtc);
    }

    public async Task<ProjectDto> UpdateAsync(Guid id, SaveProjectRequest request, CancellationToken ct)
    {
        var project = await FindProjectAsync(id, ct);
        project.Name = ValidateName(request.Name);
        project.Description = Clean(request.Description);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task SetArchivedAsync(Guid id, bool archived, CancellationToken ct)
    {
        var project = await FindProjectAsync(id, ct);
        project.IsArchived = archived;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var project = await FindProjectAsync(id, ct);
        db.Projects.Remove(project); // tasks cascade
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<TaskDto>> ListTasksAsync(Guid projectId, CancellationToken ct)
    {
        await FindProjectAsync(projectId, ct);
        var tasks = await db.Tasks
            .Where(t => t.ProjectId == projectId)
            .OrderBy(t => t.CreatedAtUtc)
            .ToListAsync(ct);
        return tasks.Select(ToDto).ToList();
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
            Title = ValidateTitle(request.Title),
            Description = Clean(request.Description),
            AssigneeUserId = request.AssigneeUserId,
            DueDate = request.DueDate
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync(ct);
        return ToDto(task);
    }

    public async Task<TaskDto> UpdateTaskAsync(Guid projectId, Guid taskId, UpdateTaskRequest request, CancellationToken ct)
    {
        var task = await FindTaskAsync(projectId, taskId, ct);
        if (!Enum.TryParse<ProjectTaskStatus>(request.Status, ignoreCase: true, out var status) || !Enum.IsDefined(status))
            throw new InvalidOperationException($"Status must be one of: {string.Join(", ", Enum.GetNames<ProjectTaskStatus>())}.");
        await EnsureAssigneeInTenantAsync(request.AssigneeUserId, ct);

        task.Title = ValidateTitle(request.Title);
        task.Description = Clean(request.Description);
        task.Status = status;
        task.AssigneeUserId = request.AssigneeUserId;
        task.DueDate = request.DueDate;
        await db.SaveChangesAsync(ct);
        return ToDto(task);
    }

    public async Task DeleteTaskAsync(Guid projectId, Guid taskId, CancellationToken ct)
    {
        var task = await FindTaskAsync(projectId, taskId, ct);
        db.Tasks.Remove(task);
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

    private static string ValidateName(string? name)
    {
        var n = name?.Trim();
        if (string.IsNullOrEmpty(n)) throw new InvalidOperationException("Project name is required.");
        if (n.Length > 200) throw new InvalidOperationException("Project name must be 200 characters or fewer.");
        return n;
    }

    private static string ValidateTitle(string? title)
    {
        var t = title?.Trim();
        if (string.IsNullOrEmpty(t)) throw new InvalidOperationException("Task title is required.");
        if (t.Length > 300) throw new InvalidOperationException("Task title must be 300 characters or fewer.");
        return t;
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static TaskDto ToDto(ProjectTask t) =>
        new(t.Id, t.ProjectId, t.Title, t.Description, t.Status.ToString(), t.AssigneeUserId, t.DueDate);
}
