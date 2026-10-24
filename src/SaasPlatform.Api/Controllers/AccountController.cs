using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaasPlatform.Application.Account;
using SaasPlatform.Domain.Common;

namespace SaasPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/account")]
public class AccountController(IAccountService account) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ProfileDto>> Get(CancellationToken ct) => Ok(await account.GetProfileAsync(ct));

    [HttpPut("profile")]
    public async Task<ActionResult<ProfileDto>> UpdateProfile(UpdateProfileRequest request, CancellationToken ct)
        => Ok(await account.UpdateProfileAsync(request, ct));

    [HttpPut("company")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ProfileDto>> UpdateCompany(UpdateCompanyRequest request, CancellationToken ct)
        => Ok(await account.UpdateCompanyAsync(request, ct));
}
