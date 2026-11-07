using Almer.Infrastructure;

namespace Almer.Tests;

public sealed class MfaSecurityTests
{
    [Fact]
    public void Totp_matches_rfc_6238_sha1_vector_truncated_to_six_digits()
    {
        const string secret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";
        Assert.Equal("287082", MfaSecurity.ComputeTotp(secret, 59));
        Assert.True(MfaSecurity.VerifyTotp(secret, "287082", DateTimeOffset.FromUnixTimeSeconds(59)));
    }

    [Fact]
    public void Totp_accepts_one_time_step_clock_skew_but_rejects_unrelated_code()
    {
        var secret = MfaSecurity.GenerateSecret();
        var time = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        var previousCode = MfaSecurity.ComputeTotp(secret, time.ToUnixTimeSeconds() - 30);
        var unrelatedCode = previousCode == "000000" ? "000001" : "000000";
        Assert.True(MfaSecurity.VerifyTotp(secret, previousCode, time));
        Assert.False(MfaSecurity.VerifyTotp(secret, unrelatedCode, time));
    }

    [Fact]
    public void Recovery_code_is_hashed_and_consumed_only_once()
    {
        var userId = Guid.NewGuid();
        var code = MfaSecurity.GenerateRecoveryCodes(1).Single();
        var hashes = new List<string> { MfaSecurity.HashRecoveryCode(userId, code) };
        Assert.DoesNotContain(code.Replace("-", ""), hashes[0], StringComparison.OrdinalIgnoreCase);
        Assert.True(MfaSecurity.TryUseRecoveryCode(userId, code.ToLowerInvariant(), hashes));
        Assert.Empty(hashes);
        Assert.False(MfaSecurity.TryUseRecoveryCode(userId, code, hashes));
    }
}
