using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaasPlatform.Application.Activity;
using SaasPlatform.Application.Dashboard;

namespace SaasPlatform.Api.Controllers;

/// <summary>Read-only views over the company's data: dashboard numbers and the activity feed.</summary>
[ApiController]
[Authorize]
[Route("api")]
public class InsightsController(IDashboardService dashboard, IActivityService activity) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardDto>> Dashboard(CancellationToken ct) => Ok(await dashboard.GetAsync(ct));

    [HttpGet("activity")]
    public async Task<ActionResult<List<ActivityDto>>> Activity([FromQuery] int take = 20, CancellationToken ct = default)
        => Ok(await activity.RecentAsync(take, ct));
}
