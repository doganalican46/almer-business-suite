namespace Almer.Application;

public static class JobRetryPolicy
{
    public static int DelaySeconds(int attempt) => Math.Min(300, 5 * (int)Math.Pow(2, Math.Clamp(attempt - 1, 0, 6)));
}
