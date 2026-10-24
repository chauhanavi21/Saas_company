using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaasPlatform.Application.Team;
using SaasPlatform.Domain.Common;

namespace SaasPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/team")]
public class TeamController(ITeamService team) : ControllerBase
{
    /// <summary>Any member of the company can see the team.</summary>
    [HttpGet]
    public async Task<ActionResult<List<TeamMemberDto>>> List(CancellationToken ct)
        => Ok(await team.ListAsync(ct));

    [HttpPut("{id:guid}/role")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<TeamMemberDto>> ChangeRole(Guid id, ChangeRoleRequest request, CancellationToken ct)
        => Ok(await team.ChangeRoleAsync(id, request.Role, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Remove(Guid id, CancellationToken ct)
    {
        await team.RemoveAsync(id, ct);
        return NoContent();
    }
}
