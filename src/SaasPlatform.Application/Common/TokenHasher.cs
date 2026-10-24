using System.Security.Cryptography;
using System.Text;

namespace SaasPlatform.Application.Common;

/// <summary>Opaque random tokens (invites, refresh tokens). Only the SHA-256 hash is ever stored.</summary>
public static class TokenHasher
{
    public static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
