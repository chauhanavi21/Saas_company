using Microsoft.EntityFrameworkCore;
using SaasPlatform.Application.Common;

namespace SaasPlatform.Application.Account;

public record ProfileDto(Guid Id, string Email, string FullName, string Role, string CompanyName, DateTime MemberSinceUtc);
public record UpdateProfileRequest(string FullName);
public record UpdateCompanyRequest(string Name);

public interface IAccountService
{
    Task<ProfileDto> GetProfileAsync(CancellationToken ct);
    Task<ProfileDto> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct);
    Task<ProfileDto> UpdateCompanyAsync(UpdateCompanyRequest request, CancellationToken ct);
}

public class AccountService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ICurrentTenant currentTenant,
    IActivityRecorder activity) : IAccountService
{
    public async Task<ProfileDto> GetProfileAsync(CancellationToken ct)
    {
        var id = currentUser.UserId ?? throw new InvalidOperationException("No user in context.");
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new KeyNotFoundException("User not found.");
        return await ToDtoAsync(user.Id, user.Email, user.FullName, user.Role, user.CreatedAtUtc, ct);
    }

    public async Task<ProfileDto> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct)
    {
        var id = currentUser.UserId ?? throw new InvalidOperationException("No user in context.");
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new KeyNotFoundException("User not found.");

        user.FullName = request.FullName.Trim();
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(user.Id, user.Email, user.FullName, user.Role, user.CreatedAtUtc, ct);
    }

    public async Task<ProfileDto> UpdateCompanyAsync(UpdateCompanyRequest request, CancellationToken ct)
    {
        var tenantId = currentTenant.TenantId ?? throw new InvalidOperationException("No tenant in context.");
        var name = request.Name.Trim();
        if (name.Length == 0) throw new InvalidOperationException("Company name is required.");
        if (name.Length > 200) throw new InvalidOperationException("Company name must be 200 characters or fewer.");

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new KeyNotFoundException("Company not found.");
        tenant.Name = name;
        activity.Record("company.renamed", "Tenant", tenant.Id, name);
        await db.SaveChangesAsync(ct);
        return await GetProfileAsync(ct);
    }

    private async Task<ProfileDto> ToDtoAsync(Guid id, string email, string fullName, string role, DateTime created, CancellationToken ct)
    {
        var tenantId = currentTenant.TenantId ?? throw new InvalidOperationException("No tenant in context.");
        var company = await db.Tenants.Where(t => t.Id == tenantId).Select(t => t.Name).FirstAsync(ct);
        return new ProfileDto(id, email, fullName, role, company, created);
    }
}
