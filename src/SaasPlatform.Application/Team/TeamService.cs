using Microsoft.EntityFrameworkCore;
using SaasPlatform.Application.Common;
using SaasPlatform.Domain.Common;

namespace SaasPlatform.Application.Team;

public record TeamMemberDto(Guid Id, string Email, string FullName, string Role);
public record ChangeRoleRequest(string Role);

public interface ITeamService
{
    Task<List<TeamMemberDto>> ListAsync(CancellationToken ct);
    Task<TeamMemberDto> ChangeRoleAsync(Guid userId, string role, CancellationToken ct);
    Task RemoveAsync(Guid userId, CancellationToken ct);
}

/// <summary>All queries here are automatically scoped to the caller's tenant by the global query filter.</summary>
public class TeamService(IApplicationDbContext db, ICurrentUser currentUser) : ITeamService
{
    public Task<List<TeamMemberDto>> ListAsync(CancellationToken ct) =>
        db.Users
            .OrderBy(u => u.FullName)
            .Select(u => new TeamMemberDto(u.Id, u.Email, u.FullName, u.Role))
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
        await db.SaveChangesAsync(ct);
        return new TeamMemberDto(user.Id, user.Email, user.FullName, user.Role);
    }

    public async Task RemoveAsync(Guid userId, CancellationToken ct)
    {
        if (userId == currentUser.UserId)
            throw new InvalidOperationException("You can't remove yourself.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        if (user.Role == Roles.Admin)
            await EnsureAnotherAdminExistsAsync(user.Id, ct);

        db.Users.Remove(user);
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureAnotherAdminExistsAsync(Guid exceptUserId, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Role == Roles.Admin && u.Id != exceptUserId, ct))
            throw new InvalidOperationException("A company must keep at least one admin.");
    }
}
