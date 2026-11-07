namespace Almer.Application;

public static class NotificationRules
{
    public static bool IsCritical(decimal balance, decimal productMinimum, int defaultThreshold) => balance <= (productMinimum > 0 ? productMinimum : defaultThreshold);
    public static bool IsExpiring(DateOnly? expiresOn, DateOnly today, int warningDays) => expiresOn.HasValue && expiresOn.Value >= today && expiresOn.Value <= today.AddDays(warningDays);
}
