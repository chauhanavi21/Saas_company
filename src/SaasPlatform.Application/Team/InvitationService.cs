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
    IAuthService auth,
    IActivityRecorder activity) : IInvitationService
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    public async Task<CreatedInvitationDto> CreateAsync(CreateInvitationRequest request, CancellationToken ct)
    {
        var tenantId = currentTenant.TenantId ?? throw new InvalidOperationException("No tenant in context.");
        var inviterId = currentUser.UserId ?? throw new InvalidOperationException("No user in context.");
        var email = request.Email.Trim().ToLowerInvariant();

        // Emails are unique across the whole platform (one account per email).
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email, ct))
            throw new InvalidOperationException("That email already has an account.");

        // Replace any still-pending invite for the same email in this tenant.
        var now = DateTime.UtcNow;
        var stale = await db.Invitations
            .Where(i => i.Email == email && i.AcceptedAtUtc == null && i.ExpiresAtUtc > now)
            .ToListAsync(ct);
        db.Invitations.RemoveRange(stale);

        var token = TokenHasher.NewToken();
        var invitation = new Invitation
        {
            TenantId = tenantId,
            Email = email,
            Role = request.Role,
            TokenHash = TokenHasher.Hash(token),
            ExpiresAtUtc = now.Add(Lifetime),
            InvitedByUserId = inviterId
        };
        db.Invitations.Add(invitation);
        activity.Record("invitation.created", "Invitation", invitation.Id, $"{email} as {request.Role}");
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
        activity.Record("invitation.revoked", "Invitation", invitation.Id, invitation.Email);
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
        // No signed-in caller here, so write the entry directly with the new member as the actor.
        db.ActivityLog.Add(new ActivityEntry
        {
            TenantId = user.TenantId,
            ActorUserId = user.Id,
            ActorName = user.FullName,
            Action = "invitation.accepted",
            EntityType = "User",
            EntityId = user.Id,
            Summary = $"{user.FullName} joined as {user.Role}"
        });
        await db.SaveChangesAsync(ct);

        return await auth.IssueAsync(user, ct);
    }

    /// <summary>Unauthenticated lookup: the secret token is the credential, so we search across tenants by hash.</summary>
    private Task<Invitation?> FindPendingAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token)) return Task.FromResult<Invitation?>(null);
        var hash = TokenHasher.Hash(token.Trim());
        var now = DateTime.UtcNow;
        return db.Invitations.IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.TokenHash == hash && i.AcceptedAtUtc == null && i.ExpiresAtUtc > now, ct);
    }

    private static InvitationDto ToDto(Invitation i) => new(i.Id, i.Email, i.Role, i.ExpiresAtUtc, i.IsPending);
}
