using System.Security.Cryptography;
using System.Text;

namespace Almer.Infrastructure;

public static class RefreshTokenSecurity
{
    public static string Generate() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
