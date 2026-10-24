using Microsoft.EntityFrameworkCore;
using SaasPlatform.Application.Common;
using SaasPlatform.Domain.Common;

namespace SaasPlatform.Application.Team;

public record TeamMemberDto(Guid Id, string Email, string FullName, string Role, DateTime JoinedAtUtc);
public record ChangeRoleRequest(string Role);

public interface ITeamService
{
    Task<List<TeamMemberDto>> ListAsync(CancellationToken ct);
    Task<TeamMemberDto> ChangeRoleAsync(Guid userId, string role, CancellationToken ct);
    Task RemoveAsync(Guid userId, CancellationToken ct);
}

/// <summary>All queries here are automatically scoped to the caller's tenant by the global query filter.</summary>
public class TeamService(IApplicationDbContext db, ICurrentUser currentUser, IActivityRecorder activity) : ITeamService
{
    public Task<List<TeamMemberDto>> ListAsync(CancellationToken ct) =>
        db.Users
            .OrderBy(u => u.FullName)
            .Select(u => new TeamMemberDto(u.Id, u.Email, u.FullName, u.Role, u.CreatedAtUtc))
            .ToListAsync(ct);

    public async Task<TeamMemberDto> ChangeRoleAsync(Guid userId, string role, CancellationToken ct)
    {
        if (!Roles.IsValid(role))
            throw new InvalidOperationException($"Role must be one of: {string.Join(", ", Roles.All)}.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        if (user.Role == Roles.Admin && role != Roles.Admin)
            await EnsureAnotherAdminExistsAsync(user.Id, ct);

        user.Role = role;
        activity.Record("member.role_changed", "User", user.Id, $"{user.FullName} is now {role}");
        await db.SaveChangesAsync(ct);
        return new TeamMemberDto(user.Id, user.Email, user.FullName, user.Role, user.CreatedAtUtc);
    }

    public async Task RemoveAsync(Guid userId, CancellationToken ct)
    {
        if (userId == currentUser.UserId)
            throw new InvalidOperationException("You can't remove yourself.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        if (user.Role == Roles.Admin)
            await EnsureAnotherAdminExistsAsync(user.Id, ct);

        // Work assigned to them goes back to the pool instead of dangling on a person who can't see it.
        var assigned = await db.Tasks.Where(t => t.AssigneeUserId == userId).ToListAsync(ct);
        foreach (var t in assigned) t.AssigneeUserId = null;

        // End their sessions so a removed person can't keep refreshing into the company.
        var sessions = await db.RefreshTokens.Where(r => r.UserId == userId && r.RevokedAtUtc == null).ToListAsync(ct);
        foreach (var s in sessions) s.RevokedAtUtc = DateTime.UtcNow;

        activity.Record("member.removed", "User", user.Id, user.FullName);
        db.Users.Remove(user);
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureAnotherAdminExistsAsync(Guid exceptUserId, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Role == Roles.Admin && u.Id != exceptUserId, ct))
            throw new InvalidOperationException("A company must keep at least one admin.");
    }
}
