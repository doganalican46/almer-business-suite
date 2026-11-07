using Almer.Api;

namespace Almer.Tests;

public sealed class NotificationChannelSecurityTests
{
    [Theory]
    [InlineData("http://hooks.example.com/almer")]
    [InlineData("https://localhost/almer")]
    [InlineData("https://127.0.0.1/almer")]
    [InlineData("https://hooks.example.com:8443/almer")]
    public void Unsafe_webhook_targets_are_rejected(string value)
    {
        Assert.Throws<ArgumentException>(() => NotificationChannelSecurity.ValidateWebhook(value, ["hooks.example.com"], false));
    }

    [Fact]
    public void Production_webhook_requires_exact_allowlisted_host()
    {
        Assert.Equal("hooks.example.com", NotificationChannelSecurity.ValidateWebhook("https://hooks.example.com/almer", ["hooks.example.com"], false).Host);
        Assert.Throws<ArgumentException>(() => NotificationChannelSecurity.ValidateWebhook("https://other.example.com/almer", ["hooks.example.com"], false));
    }

    [Fact]
    public void Signature_is_stable_and_payload_sensitive()
    {
        var first = NotificationChannelSecurity.ComputeSignature("a-secret-with-at-least-32-characters", "{\"value\":1}");
        Assert.Equal(first, NotificationChannelSecurity.ComputeSignature("a-secret-with-at-least-32-characters", "{\"value\":1}"));
        Assert.NotEqual(first, NotificationChannelSecurity.ComputeSignature("a-secret-with-at-least-32-characters", "{\"value\":2}"));
    }
}
