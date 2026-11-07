using Almer.Infrastructure;

namespace Almer.Tests;

public sealed class RefreshTokenSecurityTests
{
    [Fact]
    public void Generated_tokens_are_high_entropy_url_safe_and_unique()
    {
        var first = RefreshTokenSecurity.Generate(); var second = RefreshTokenSecurity.Generate();
        Assert.NotEqual(first, second); Assert.True(first.Length >= 80);
        Assert.DoesNotContain('+', first); Assert.DoesNotContain('/', first); Assert.DoesNotContain('=', first);
    }

    [Fact]
    public void Hash_is_deterministic_without_storing_the_raw_token()
    {
        var token = RefreshTokenSecurity.Generate(); var hash = RefreshTokenSecurity.Hash(token);
        Assert.Equal(hash, RefreshTokenSecurity.Hash(token)); Assert.NotEqual(token, hash); Assert.Equal(64, hash.Length);
    }
}
