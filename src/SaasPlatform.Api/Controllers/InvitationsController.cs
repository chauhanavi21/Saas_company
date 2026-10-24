using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaasPlatform.Application.Auth;
using SaasPlatform.Application.Team;
using SaasPlatform.Domain.Common;

namespace SaasPlatform.Api.Controllers;

[ApiController]
[Route("api/invitations")]
public class InvitationsController(IInvitationService invitations) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<CreatedInvitationDto>> Create(CreateInvitationRequest request, CancellationToken ct)
        => Ok(await invitations.CreateAsync(request, ct));

    [HttpGet]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<List<InvitationDto>>> ListPending(CancellationToken ct)
        => Ok(await invitations.ListPendingAsync(ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        await invitations.RevokeAsync(id, ct);
        return NoContent();
    }

    /// <summary>Public: lets the invite page show the company name before the invitee signs up.</summary>
    [HttpGet("preview")]
    [AllowAnonymous]
    public async Task<ActionResult<InvitationPreviewDto>> Preview([FromQuery] string token, CancellationToken ct)
    {
        var preview = await invitations.PreviewAsync(token, ct);
        return preview is null ? NotFound(new { title = "This invitation is invalid or has expired." }) : Ok(preview);
    }

    [HttpPost("accept")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Accept(AcceptInvitationRequest request, CancellationToken ct)
        => Ok(await invitations.AcceptAsync(request, ct));
}
