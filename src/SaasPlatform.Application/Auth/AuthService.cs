using Microsoft.EntityFrameworkCore;
using SaasPlatform.Application.Common;
using SaasPlatform.Domain.Common;
using SaasPlatform.Domain.Entities;

namespace SaasPlatform.Application.Auth;

public record RegisterRequest(string CompanyName, string FullName, string Email, string Password);
public record LoginRequest(string Email, string Password);
public record AuthResponse(string Token, Guid TenantId, string Email, string FullName, string Role);

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken ct);
}

public class AuthService(IApplicationDbContext db, IPasswordHasher hasher, IJwtTokenService jwt) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (request.Password.Length < 8)
            throw new InvalidOperationException("Password must be at least 8 characters.");
        if (string.IsNullOrWhiteSpace(request.CompanyName) || string.IsNullOrWhiteSpace(request.FullName))
            throw new InvalidOperationException("Company name and full name are required.");

        // Unauthenticated: there is no current tenant, so look across all tenants.
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email, ct))
            throw new InvalidOperationException("Email is already registered.");

        var tenant = new Tenant
        {
            Name = request.CompanyName.Trim(),
            Slug = request.CompanyName.Trim().ToLowerInvariant().Replace(' ', '-')
        };
        var user = new AppUser
        {
            TenantId = tenant.Id,
            Email = email,
            FullName = request.FullName.Trim(),
            PasswordHash = hasher.Hash(request.Password),
            Role = Roles.Admin
        };

        db.Tenants.Add(tenant);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return ToResponse(user);
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == email, ct);

        return user is not null && hasher.Verify(request.Password, user.PasswordHash)
            ? ToResponse(user)
            : null;
    }

    private AuthResponse ToResponse(AppUser u) => new(jwt.CreateToken(u), u.TenantId, u.Email, u.FullName, u.Role);
}
