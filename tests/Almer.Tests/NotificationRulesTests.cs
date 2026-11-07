using Almer.Application;

namespace Almer.Tests;

public sealed class NotificationRulesTests
{
    [Theory]
    [InlineData(4, 5, 10, true)]
    [InlineData(6, 5, 10, false)]
    [InlineData(9, 0, 10, true)]
    [InlineData(11, 0, 10, false)]
    public void Critical_stock_uses_product_minimum_then_tenant_default(decimal balance, decimal productMinimum, int tenantDefault, bool expected) => Assert.Equal(expected, NotificationRules.IsCritical(balance, productMinimum, tenantDefault));

    [Fact]
    public void Expiry_warning_respects_window_and_ignores_already_expired_lots()
    {
        var today = new DateOnly(2026, 11, 6);
        Assert.True(NotificationRules.IsExpiring(today.AddDays(7), today, 14));
        Assert.False(NotificationRules.IsExpiring(today.AddDays(15), today, 14));
        Assert.False(NotificationRules.IsExpiring(today.AddDays(-1), today, 14));
        Assert.False(NotificationRules.IsExpiring(null, today, 14));
    }
}
