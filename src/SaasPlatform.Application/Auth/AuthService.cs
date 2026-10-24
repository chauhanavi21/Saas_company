using Microsoft.EntityFrameworkCore;
using SaasPlatform.Application.Common;
using SaasPlatform.Domain.Common;
using SaasPlatform.Domain.Entities;

namespace SaasPlatform.Application.Auth;

public record RegisterRequest(string CompanyName, string FullName, string Email, string Password);
public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record LogoutRequest(string RefreshToken);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record AuthResponse(
    string Token,
    string RefreshToken,
    Guid TenantId,
    Guid UserId,
    string Email,
    string FullName,
    string Role,
    string CompanyName);

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<AuthResponse?> RefreshAsync(string refreshToken, CancellationToken ct);
    Task LogoutAsync(string refreshToken, CancellationToken ct);
    Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct);

    /// <summary>Issues an access + refresh token pair for a user (used by login, register and invite acceptance).</summary>
    Task<AuthResponse> IssueAsync(AppUser user, CancellationToken ct);
}

public class AuthService(
    IApplicationDbContext db,
    IPasswordHasher hasher,
    IJwtTokenService jwt,
    ICurrentUser currentUser) : IAuthService
{
    public static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(14);

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        // Unauthenticated: there is no current tenant, so look across all tenants.
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email, ct))
            throw new InvalidOperationException("Email is already registered.");

        var name = request.CompanyName.Trim();
        var tenant = new Tenant { Name = name, Slug = await UniqueSlugAsync(name, ct) };
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
        db.ActivityLog.Add(new ActivityEntry
        {
            TenantId = tenant.Id,
            ActorUserId = user.Id,
            ActorName = user.FullName,
            Action = "company.created",
            EntityType = "Tenant",
            EntityId = tenant.Id,
            Summary = tenant.Name
        });
        await db.SaveChangesAsync(ct);

        return await IssueAsync(user, ct);
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == email, ct);

        // Verify even when the user is missing so response time doesn't reveal which emails exist.
        var hash = user?.PasswordHash ?? DummyHash;
        var ok = hasher.Verify(request.Password, hash);
        return user is not null && ok ? await IssueAsync(user, ct) : null;
    }

    public async Task<AuthResponse?> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return null;
        var hash = TokenHasher.Hash(refreshToken.Trim());
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash, ct);
        if (stored is null) return null;

        // A rotated-out or revoked token showing up again means it was copied. Kill every session for that user.
        if (stored.RevokedAtUtc is not null)
        {
            await RevokeAllAsync(stored.UserId, ct);
            return null;
        }
        if (stored.ExpiresAtUtc <= DateTime.UtcNow) return null;

        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == stored.UserId, ct);
        if (user is null) return null;

        var response = await IssueAsync(user, ct);
        stored.RevokedAtUtc = DateTime.UtcNow;
        stored.ReplacedByTokenHash = TokenHasher.Hash(response.RefreshToken);
        await db.SaveChangesAsync(ct);
        return response;
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;
        var hash = TokenHasher.Hash(refreshToken.Trim());
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash, ct);
        if (stored is { RevokedAtUtc: null })
        {
            stored.RevokedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new InvalidOperationException("No user in context.");
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        if (!hasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw new InvalidOperationException("Your current password is incorrect.");

        user.PasswordHash = hasher.Hash(request.NewPassword);
        await RevokeAllAsync(user.Id, ct); // sign out every other device
        await db.SaveChangesAsync(ct);
    }

    public async Task<AuthResponse> IssueAsync(AppUser user, CancellationToken ct)
    {
        var raw = TokenHasher.NewToken();
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = TokenHasher.Hash(raw),
            ExpiresAtUtc = DateTime.UtcNow.Add(RefreshLifetime)
        });
        await db.SaveChangesAsync(ct);

        var company = await db.Tenants.Where(t => t.Id == user.TenantId).Select(t => t.Name).FirstAsync(ct);
        return new AuthResponse(jwt.CreateToken(user), raw, user.TenantId, user.Id, user.Email, user.FullName, user.Role, company);
    }

    private async Task RevokeAllAsync(Guid userId, CancellationToken ct)
    {
        var active = await db.RefreshTokens.Where(r => r.UserId == userId && r.RevokedAtUtc == null).ToListAsync(ct);
        foreach (var t in active) t.RevokedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task<string> UniqueSlugAsync(string name, CancellationToken ct)
    {
        var baseSlug = new string(name.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        while (baseSlug.Contains("--")) baseSlug = baseSlug.Replace("--", "-");
        if (baseSlug.Length == 0) baseSlug = "company";

        var slug = baseSlug;
        for (var i = 2; await db.Tenants.AnyAsync(t => t.Slug == slug, ct); i++) slug = $"{baseSlug}-{i}";
        return slug;
    }

    /// <summary>A valid-format hash of a random password, used only to equalise login timing for unknown emails.</summary>
    private string DummyHash => dummy ??= hasher.Hash(Guid.NewGuid().ToString());
    private static string? dummy;
}
