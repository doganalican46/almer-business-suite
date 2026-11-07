using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Almer.Infrastructure;

public static class MfaSecurity
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string GenerateSecret() => Base32Encode(RandomNumberGenerator.GetBytes(20));

    public static bool VerifyTotp(string secret, string code, DateTimeOffset? now = null)
    {
        var normalized = new string(code.Where(char.IsDigit).ToArray());
        if (normalized.Length != 6) return false;
        var unixSeconds = (now ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
        for (var offset = -1; offset <= 1; offset++)
        {
            var expected = ComputeTotp(secret, unixSeconds + offset * 30);
            if (CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(normalized))) return true;
        }
        return false;
    }

    public static string ComputeTotp(string secret, long unixSeconds)
    {
        var key = Base32Decode(secret);
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, unixSeconds / 30);
        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counter.ToArray());
        var offset = hash[^1] & 0x0f;
        var value = ((hash[offset] & 0x7f) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (value % 1_000_000).ToString("D6");
    }

    public static IReadOnlyList<string> GenerateRecoveryCodes(int count = 8) => Enumerable.Range(0, count)
        .Select(_ => Convert.ToHexString(RandomNumberGenerator.GetBytes(8)))
        .Select(x => $"{x[..4]}-{x[4..8]}-{x[8..12]}-{x[12..]}")
        .ToList();

    public static string HashRecoveryCode(Guid userId, string code)
    {
        var normalized = new string(code.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{userId:N}:{normalized}")));
    }

    public static bool TryUseRecoveryCode(Guid userId, string code, IList<string> hashes)
    {
        var candidate = HashRecoveryCode(userId, code);
        var candidateBytes = Convert.FromHexString(candidate);
        for (var i = 0; i < hashes.Count; i++)
        {
            byte[] stored;
            try { stored = Convert.FromHexString(hashes[i]); }
            catch (FormatException) { continue; }
            if (!CryptographicOperations.FixedTimeEquals(candidateBytes, stored)) continue;
            hashes.RemoveAt(i);
            return true;
        }
        return false;
    }

    private static string Base32Encode(byte[] data)
    {
        var output = new StringBuilder((data.Length * 8 + 4) / 5);
        var buffer = 0; var bits = 0;
        foreach (var value in data)
        {
            buffer = (buffer << 8) | value; bits += 8;
            while (bits >= 5) { bits -= 5; output.Append(Alphabet[(buffer >> bits) & 31]); }
        }
        if (bits > 0) output.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        return output.ToString();
    }

    private static byte[] Base32Decode(string value)
    {
        var bytes = new List<byte>(); var buffer = 0; var bits = 0;
        foreach (var c in value.Trim().TrimEnd('=').ToUpperInvariant())
        {
            var index = Alphabet.IndexOf(c);
            if (index < 0) throw new FormatException("Invalid Base32 secret.");
            buffer = (buffer << 5) | index; bits += 5;
            if (bits < 8) continue;
            bits -= 8; bytes.Add((byte)(buffer >> bits)); buffer &= (1 << bits) - 1;
        }
        return bytes.ToArray();
    }
}
