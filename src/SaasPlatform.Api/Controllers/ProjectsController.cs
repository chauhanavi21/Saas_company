using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaasPlatform.Application.Projects;
using SaasPlatform.Domain.Common;

namespace SaasPlatform.Api.Controllers;

/// <summary>Members can create and edit; only admins can delete.</summary>
[ApiController]
[Authorize]
[Route("api/projects")]
public class ProjectsController(IProjectService projects) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ProjectDto>>> List([FromQuery] bool includeArchived, CancellationToken ct)
        => Ok(await projects.ListAsync(includeArchived, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProjectDto>> Get(Guid id, CancellationToken ct)
        => Ok(await projects.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<ProjectDto>> Create(SaveProjectRequest request, CancellationToken ct)
    {
        var created = await projects.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProjectDto>> Update(Guid id, SaveProjectRequest request, CancellationToken ct)
        => Ok(await projects.UpdateAsync(id, request, ct));

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        await projects.SetArchivedAsync(id, true, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/unarchive")]
    public async Task<IActionResult> Unarchive(Guid id, CancellationToken ct)
    {
        await projects.SetArchivedAsync(id, false, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await projects.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/tasks")]
    public async Task<ActionResult<List<TaskDto>>> ListTasks(Guid id, CancellationToken ct)
        => Ok(await projects.ListTasksAsync(id, ct));

    [HttpPost("{id:guid}/tasks")]
    public async Task<ActionResult<TaskDto>> CreateTask(Guid id, CreateTaskRequest request, CancellationToken ct)
        => Ok(await projects.CreateTaskAsync(id, request, ct));

    [HttpPut("{id:guid}/tasks/{taskId:guid}")]
    public async Task<ActionResult<TaskDto>> UpdateTask(Guid id, Guid taskId, UpdateTaskRequest request, CancellationToken ct)
        => Ok(await projects.UpdateTaskAsync(id, taskId, request, ct));

    [HttpDelete("{id:guid}/tasks/{taskId:guid}")]
    public async Task<IActionResult> DeleteTask(Guid id, Guid taskId, CancellationToken ct)
    {
        await projects.DeleteTaskAsync(id, taskId, ct);
        return NoContent();
    }
}
