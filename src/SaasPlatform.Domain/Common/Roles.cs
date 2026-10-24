namespace SaasPlatform.Domain.Common;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Member = "Member";

    public static readonly string[] All = [Admin, Member];

    public static bool IsValid(string? role) => role is not null && All.Contains(role);
}
