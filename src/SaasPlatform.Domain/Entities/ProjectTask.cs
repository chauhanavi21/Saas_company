using SaasPlatform.Domain.Common;

namespace SaasPlatform.Domain.Entities;

public enum ProjectTaskStatus
{
    Todo = 0,
    InProgress = 1,
    Done = 2
}

public enum TaskPriority
{
    Low = 0,
    Medium = 1,
    High = 2
}

public class ProjectTask : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ProjectTaskStatus Status { get; set; } = ProjectTaskStatus.Todo;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public Guid? AssigneeUserId { get; set; }
    public DateOnly? DueDate { get; set; }
}
