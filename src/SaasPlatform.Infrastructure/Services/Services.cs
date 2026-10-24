using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SaasPlatform.Application.Common;
using SaasPlatform.Domain.Entities;

namespace SaasPlatform.Infrastructure.Services;

public class JwtSettings
{
    public const string Section = "Jwt";
    public string Issuer { get; set; } = "SaasPlatform";
    public string Audience { get; set; } = "SaasPlatform.Web";
    public string Key { get; set; } = string.Empty;
    /// <summary>Access tokens are short-lived; the client silently swaps them using the refresh token.</summary>
    public int ExpiryMinutes { get; set; } = 15;
}

public class CurrentTenant(IHttpContextAccessor accessor) : ICurrentTenant
{
    public Guid? TenantId =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirstValue("tenant_id"), out var id) ? id : null;
}

public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser, ICurrentUserName
{
    public string? DisplayName => accessor.HttpContext?.User.FindFirstValue("name");

    public Guid? UserId =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;

    public string? Role => accessor.HttpContext?.User.FindFirstValue("role");
}

public class JwtTokenService(IOptions<JwtSettings> options) : IJwtTokenService
{
    public string CreateToken(AppUser user)
    {
        var s = options.Value;
        var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(s.Key)), SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("name", user.FullName),
            new Claim("tenant_id", user.TenantId.ToString()),
            new Claim("role", user.Role)
        };
        var token = new JwtSecurityToken(s.Issuer, s.Audience, claims,
            expires: DateTime.UtcNow.AddMinutes(s.ExpiryMinutes), signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

/// <summary>PBKDF2-SHA256 with a per-password salt. Format: iterations.salt.hash (base64).</summary>
public class PasswordHasher : IPasswordHasher
{
    private const int Iterations = 210_000, SaltSize = 16, HashSize = 32;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string stored)
    {
        var parts = stored.Split('.');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations)) return false;
        var expected = Convert.FromBase64String(parts[2]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(parts[1]), iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
