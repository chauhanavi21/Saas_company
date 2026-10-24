using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SaasPlatform.Application.Auth;
using SaasPlatform.Application.Common;
using SaasPlatform.Domain.Common;
using SaasPlatform.Domain.Entities;

namespace SaasPlatform.Application.Team;

public record CreateInvitationRequest(string Email, string Role);
public record InvitationDto(Guid Id, string Email, string Role, DateTime ExpiresAtUtc, bool IsPending);
/// <summary>Returned once at creation. The token is not recoverable afterwards.</summary>
public record CreatedInvitationDto(InvitationDto Invitation, string Token);
public record InvitationPreviewDto(string CompanyName, string Email, string Role);
public record AcceptInvitationRequest(string Token, string FullName, string Password);

public interface IInvitationService
{
    Task<CreatedInvitationDto> CreateAsync(CreateInvitationRequest request, CancellationToken ct);
    Task<List<InvitationDto>> ListPendingAsync(CancellationToken ct);
    Task RevokeAsync(Guid id, CancellationToken ct);
    Task<InvitationPreviewDto?> PreviewAsync(string token, CancellationToken ct);
    Task<AuthResponse> AcceptAsync(AcceptInvitationRequest request, CancellationToken ct);
}

public class InvitationService(
    IApplicationDbContext db,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser,
    IPasswordHasher hasher,
    IJwtTokenService jwt) : IInvitationService
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    public async Task<CreatedInvitationDto> CreateAsync(CreateInvitationRequest request, CancellationToken ct)
    {
        var tenantId = currentTenant.TenantId ?? throw new InvalidOperationException("No tenant in context.");
        var inviterId = currentUser.UserId ?? throw new InvalidOperationException("No user in context.");
        var email = request.Email.Trim().ToLowerInvariant();

        if (!email.Contains('@')) throw new InvalidOperationException("Enter a valid email address.");
        if (!Roles.IsValid(request.Role)) throw new InvalidOperationException($"Role must be one of: {string.Join(", ", Roles.All)}.");

        // Emails are unique across the whole platform (one account per email).
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email, ct))
            throw new InvalidOperationException("That email already has an account.");

        // Replace any still-pending invite for the same email in this tenant.
        var now = DateTime.UtcNow;
        var stale = await db.Invitations
            .Where(i => i.Email == email && i.AcceptedAtUtc == null && i.ExpiresAtUtc > now)
            .ToListAsync(ct);
        db.Invitations.RemoveRange(stale);

        var token = WebSafeToken();
        var invitation = new Invitation
        {
            TenantId = tenantId,
            Email = email,
            Role = request.Role,
            TokenHash = Hash(token),
            ExpiresAtUtc = now.Add(Lifetime),
            InvitedByUserId = inviterId
        };
        db.Invitations.Add(invitation);
        await db.SaveChangesAsync(ct);

        return new CreatedInvitationDto(ToDto(invitation), token);
    }

    public async Task<List<InvitationDto>> ListPendingAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var items = await db.Invitations
            .Where(i => i.AcceptedAtUtc == null && i.ExpiresAtUtc > now)
            .OrderByDescending(i => i.CreatedAtUtc)
            .ToListAsync(ct);
        return items.Select(ToDto).ToList();
    }

    public async Task RevokeAsync(Guid id, CancellationToken ct)
    {
        var invitation = await db.Invitations.FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new KeyNotFoundException("Invitation not found.");
        db.Invitations.Remove(invitation);
        await db.SaveChangesAsync(ct);
    }

    public async Task<InvitationPreviewDto?> PreviewAsync(string token, CancellationToken ct)
    {
        var invitation = await FindPendingAsync(token, ct);
        if (invitation is null) return null;

        var tenantName = await db.Tenants.Where(t => t.Id == invitation.TenantId).Select(t => t.Name).FirstAsync(ct);
        return new InvitationPreviewDto(tenantName, invitation.Email, invitation.Role);
    }

    public async Task<AuthResponse> AcceptAsync(AcceptInvitationRequest request, CancellationToken ct)
    {
        if (request.Password.Length < 8)
            throw new InvalidOperationException("Password must be at least 8 characters.");
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new InvalidOperationException("Your name is required.");

        var invitation = await FindPendingAsync(request.Token, ct)
            ?? throw new InvalidOperationException("This invitation is invalid or has expired.");

        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == invitation.Email, ct))
            throw new InvalidOperationException("That email already has an account.");

        var user = new AppUser
        {
            TenantId = invitation.TenantId,
            Email = invitation.Email,
            FullName = request.FullName.Trim(),
            PasswordHash = hasher.Hash(request.Password),
            Role = invitation.Role
        };
        invitation.AcceptedAtUtc = DateTime.UtcNow;

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return new AuthResponse(jwt.CreateToken(user), user.TenantId, user.Email, user.FullName, user.Role);
    }

    /// <summary>Unauthenticated lookup: the secret token is the credential, so we search across tenants by hash.</summary>
    private Task<Invitation?> FindPendingAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token)) return Task.FromResult<Invitation?>(null);
        var hash = Hash(token.Trim());
        var now = DateTime.UtcNow;
        return db.Invitations.IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.TokenHash == hash && i.AcceptedAtUtc == null && i.ExpiresAtUtc > now, ct);
    }

    private static InvitationDto ToDto(Invitation i) => new(i.Id, i.Email, i.Role, i.ExpiresAtUtc, i.IsPending);

    private static string WebSafeToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
