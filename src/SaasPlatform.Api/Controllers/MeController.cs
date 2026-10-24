using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaasPlatform.Application.Common;

namespace SaasPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/me")]
public class MeController(IApplicationDbContext db) : ControllerBase
{
    /// <summary>Tenant-filtered automatically: only returns users from the caller's own tenant.</summary>
    [HttpGet("team")]
    public async Task<IActionResult> Team(CancellationToken ct)
        => Ok(await db.Users.Select(u => new { u.Id, u.Email, u.FullName, u.Role }).ToListAsync(ct));
}
