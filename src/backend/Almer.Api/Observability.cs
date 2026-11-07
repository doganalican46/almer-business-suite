using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Claims;
using Almer.Application;
using Almer.Domain;
using Almer.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Almer.Api;

public sealed class OperationalMetrics
{
    private long requests; private long errors; private long durationTicks; private long completedJobs; private long failedJobs;
    private readonly ConcurrentDictionary<int, long> statuses = new();
    public void RecordRequest(int statusCode, TimeSpan duration) { Interlocked.Increment(ref requests); Interlocked.Add(ref durationTicks, duration.Ticks); statuses.AddOrUpdate(statusCode, 1, (_, value) => value + 1); if (statusCode >= 500) Interlocked.Increment(ref errors); }
    public void RecordJob(bool succeeded) { if (succeeded) Interlocked.Increment(ref completedJobs); else Interlocked.Increment(ref failedJobs); }
    public OperationalSnapshot Snapshot()
    {
        var count = Interlocked.Read(ref requests); var ticks = Interlocked.Read(ref durationTicks);
        return new(count, Interlocked.Read(ref errors), count == 0 ? 0 : TimeSpan.FromTicks(ticks / count).TotalMilliseconds, statuses.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value), Interlocked.Read(ref completedJobs), Interlocked.Read(ref failedJobs), Environment.ProcessId, Environment.WorkingSet, DateTimeOffset.UtcNow);
    }
}

public sealed record OperationalSnapshot(long Requests, long ServerErrors, double AverageDurationMs, IReadOnlyDictionary<int, long> StatusCodes, long CompletedJobs, long FailedJobs, int ProcessId, long WorkingSetBytes, DateTimeOffset CapturedAt);

public sealed class RequestTelemetryMiddleware(RequestDelegate next, ILogger<RequestTelemetryMiddleware> logger, OperationalMetrics metrics)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp(); var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier; context.Response.Headers["X-Trace-Id"] = traceId;
        try { await next(context); }
        finally
        {
            var duration = Stopwatch.GetElapsedTime(started); metrics.RecordRequest(context.Response.StatusCode, duration);
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier); var tenantId = context.User.FindFirstValue("tenant_id");
            logger.LogInformation("HTTP {Method} {Path} responded {StatusCode} in {DurationMs} ms trace={TraceId} user={UserId} tenant={TenantId}", context.Request.Method, context.Request.Path.Value, context.Response.StatusCode, Math.Round(duration.TotalMilliseconds, 2), traceId, userId, tenantId);
        }
    }
}

public static class ObservabilityEndpoints
{
    public static void MapPlatformHealth(this WebApplication app, IConfiguration configuration)
    {
        app.MapGet("/health/live", () => Results.Ok(new { status = "ok", service = "almer-api", time = DateTimeOffset.UtcNow }));
        app.MapGet("/health/ready", async (AlmerDbContext db, CancellationToken ct) =>
        {
            try
            {
                if (!await db.Database.CanConnectAsync(ct)) return Results.Json(new { status = "not-ready", database = "unavailable" }, statusCode: 503);
                await db.TenantSet.AsNoTracking().Select(x => x.Id).Take(1).ToListAsync(ct);
                var keyPath = configuration["DataProtection:KeyPath"]; var keysReady = string.IsNullOrWhiteSpace(keyPath) || Directory.Exists(keyPath);
                return keysReady ? Results.Ok(new { status = "ready", database = "ok", dataProtection = "ok" }) : Results.Json(new { status = "not-ready", database = "ok", dataProtection = "unavailable" }, statusCode: 503);
            }
            catch { return Results.Json(new { status = "not-ready", database = "unavailable" }, statusCode: 503); }
        });
    }

    public static RouteGroupBuilder MapObservabilityEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/operations/metrics", async (OperationalMetrics metrics, AlmerDbContext db, CancellationToken ct) =>
        {
            var now = DateTimeOffset.UtcNow; var jobs = await db.BackgroundJobSet.AsNoTracking().GroupBy(x => x.Status).Select(x => new { status = x.Key, count = x.Count() }).ToListAsync(ct);
            var activeSessions = await db.UserSessionSet.AsNoTracking().CountAsync(x => x.RevokedAt == null && x.ExpiresAt > now, ct); var notifications = await db.NotificationSet.AsNoTracking().CountAsync(x => x.ResolvedAt == null, ct);
            return Results.Ok(new { runtime = metrics.Snapshot(), tenant = new { jobs, activeSessions, activeNotifications = notifications } });
        }).RequireAnyPermission(PermissionCodes.SettingsManage, PermissionCodes.AuditRead);
        group.MapGet("/operations/health", async (AlmerDbContext db, CancellationToken ct) =>
        {
            var now = DateTimeOffset.UtcNow; var queued = await db.BackgroundJobSet.AsNoTracking().CountAsync(x => x.Status == BackgroundJobStatus.Queued, ct); var running = await db.BackgroundJobSet.AsNoTracking().CountAsync(x => x.Status == BackgroundJobStatus.Running, ct); var failed24h = await db.BackgroundJobSet.AsNoTracking().CountAsync(x => x.Status == BackgroundJobStatus.Failed && x.CompletedAt > now.AddHours(-24), ct); var stale = await db.BackgroundJobSet.AsNoTracking().CountAsync(x => x.Status == BackgroundJobStatus.Running && x.LockedAt < now.AddMinutes(-15), ct);
            return Results.Ok(new { status = stale > 0 || failed24h > 0 ? "attention" : "healthy", queue = new { queued, running, failedLast24Hours = failed24h, stale }, checkedAt = now });
        }).RequireAnyPermission(PermissionCodes.SettingsManage, PermissionCodes.AuditRead);
        return group;
    }
}
