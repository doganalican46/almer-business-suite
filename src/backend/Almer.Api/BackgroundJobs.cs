using System.Text.Json;
using Almer.Application;
using Almer.Domain;
using Almer.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Almer.Api;

public static class BackgroundJobEndpoints
{
    public static RouteGroupBuilder MapBackgroundJobEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/background-jobs/{id:guid}", Get).RequirePermission(PermissionCodes.ImportsRead);
        group.MapPost("/background-jobs/{id:guid}/retry", Retry).RequirePermission(PermissionCodes.ImportsCreate);
        return group;
    }

    private static async Task<IResult> Get(Guid id, ITenantContext tenant, AlmerDbContext db, CancellationToken ct)
    {
        var job = await db.BackgroundJobSet.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Background job not found.");
        var batch = await db.ImportBatchSet.AsNoTracking().SingleOrDefaultAsync(x => x.BackgroundJobId == id, ct);
        EnsureBranch(tenant, batch?.BranchId);
        return Results.Ok(new { job.Id, job.Type, job.Status, job.Progress, job.Attempts, job.MaxAttempts, job.Error, job.CreatedAt, job.AvailableAt, job.LockedAt, job.CompletedAt, importBatchId = batch?.Id });
    }

    private static async Task<IResult> Retry(Guid id, ITenantContext tenant, AlmerDbContext db, CancellationToken ct)
    {
        var job = await db.BackgroundJobSet.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Background job not found.");
        var batch = await db.ImportBatchSet.SingleOrDefaultAsync(x => x.BackgroundJobId == id, ct) ?? throw new KeyNotFoundException("Import not found.");
        EnsureBranch(tenant, batch.BranchId);
        if (job.Status != BackgroundJobStatus.Failed) throw new InvalidOperationException("Only failed jobs can be retried.");
        job.Status = BackgroundJobStatus.Queued; job.Attempts = 0; job.Progress = 0; job.Error = null; job.LockedAt = null; job.CompletedAt = null; job.AvailableAt = DateTimeOffset.UtcNow;
        batch.Status = ImportStatus.Queued; batch.FailureMessage = null; batch.CompletedAt = null;
        await db.SaveChangesAsync(ct);
        return Results.Accepted($"/api/background-jobs/{job.Id}", new { job.Id, job.Status });
    }

    private static void EnsureBranch(ITenantContext tenant, Guid? branchId)
    {
        if (tenant.AllowedBranchIds.Count > 0 && (!branchId.HasValue || !tenant.AllowedBranchIds.Contains(branchId.Value))) throw new UnauthorizedAccessException("Branch access denied.");
    }
}

public sealed class PersistentJobWorker(IServiceScopeFactory scopeFactory, ILogger<PersistentJobWorker> logger, OperationalMetrics metrics) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private DateTimeOffset nextNotificationScan = DateTimeOffset.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverStaleJobs(stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (DateTimeOffset.UtcNow >= nextNotificationScan) { await RecoverStaleJobs(stoppingToken); await EnqueueNotificationRefreshJobs(stoppingToken); nextNotificationScan = DateTimeOffset.UtcNow.AddMinutes(5); }
                var claimed = await Claim(stoppingToken);
                if (claimed is null) { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); continue; }
                await Process(claimed, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error)
            {
                logger.LogError(error, "Persistent job worker loop failed.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task<ClaimedJob?> Claim(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var tenant = scope.ServiceProvider.GetRequiredService<TenantContext>();
        var db = scope.ServiceProvider.GetRequiredService<AlmerDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var job = await db.BackgroundJobSet.FromSqlRaw("""
            SELECT * FROM "BackgroundJobSet"
            WHERE "Status" = 0 AND "AvailableAt" <= now()
            ORDER BY CASE WHEN "Type" = 0 THEN 0 ELSE 1 END, "CreatedAt"
            FOR UPDATE SKIP LOCKED
            LIMIT 1
            """).IgnoreQueryFilters().SingleOrDefaultAsync(ct);
        if (job is null) { await transaction.RollbackAsync(ct); return null; }
        var branches = DeserializeBranches(job.BranchIdsJson);
        tenant.SetBackgroundScope(job.TenantId, job.CreatedByUserId ?? Guid.Empty, branches);
        job.Status = BackgroundJobStatus.Running; job.Attempts++; job.LockedAt = DateTimeOffset.UtcNow; job.Error = null;
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return new(job.Id, job.TenantId, job.CreatedByUserId ?? Guid.Empty, branches, job.Type, job.PayloadJson, job.Attempts, job.MaxAttempts);
    }

    private async Task Process(ClaimedJob claimed, CancellationToken ct)
    {
        using var leaseCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var heartbeat = RenewLease(claimed.Id, leaseCancellation.Token);
        try
        {
            using var scope = scopeFactory.CreateScope();
            var tenant = scope.ServiceProvider.GetRequiredService<TenantContext>(); tenant.SetBackgroundScope(claimed.TenantId, claimed.UserId, claimed.BranchIds);
            var db = scope.ServiceProvider.GetRequiredService<AlmerDbContext>();
            if (claimed.Type == BackgroundJobType.ImportCommit)
            {
                var payload = JsonSerializer.Deserialize<ImportCommitPayload>(claimed.PayloadJson, JsonOptions) ?? throw new InvalidOperationException("Invalid import job payload.");
                await ImportEndpoints.CommitBatchAsync(payload.ImportBatchId, tenant, db, ct);
            }
            else if (claimed.Type == BackgroundJobType.NotificationRefresh) await NotificationEndpoints.RefreshAsync(tenant, db, ct);
            else if (claimed.Type is BackgroundJobType.NotificationEmailDelivery or BackgroundJobType.NotificationWebhookDelivery)
            {
                var payload = JsonSerializer.Deserialize<NotificationDeliveryPayload>(claimed.PayloadJson, JsonOptions) ?? throw new InvalidOperationException("Invalid notification delivery payload.");
                await scope.ServiceProvider.GetRequiredService<NotificationDeliveryService>().DeliverAsync(claimed.Type, payload, db, ct);
            }
            else if (claimed.Type == BackgroundJobType.AccountEmailDelivery)
            {
                var payload = JsonSerializer.Deserialize<AccountEmailPayload>(claimed.PayloadJson, JsonOptions) ?? throw new InvalidOperationException("Invalid account email payload.");
                await scope.ServiceProvider.GetRequiredService<AccountEmailDeliveryService>().DeliverAsync(payload, ct);
            }
            var job = await db.BackgroundJobSet.SingleAsync(x => x.Id == claimed.Id, ct);
            job.Status = BackgroundJobStatus.Succeeded; job.Progress = 100; job.CompletedAt = DateTimeOffset.UtcNow; job.LockedAt = null;
            await db.SaveChangesAsync(ct); metrics.RecordJob(true);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            logger.LogError(error, "Background job {JobId} failed on attempt {Attempt}.", claimed.Id, claimed.Attempts);
            await MarkFailed(claimed, error, ct);
            metrics.RecordJob(false);
        }
        finally
        {
            leaseCancellation.Cancel();
            try { await heartbeat; } catch (OperationCanceledException) { }
        }
    }

    private async Task MarkFailed(ClaimedJob claimed, Exception error, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var tenant = scope.ServiceProvider.GetRequiredService<TenantContext>(); tenant.SetBackgroundScope(claimed.TenantId, claimed.UserId, claimed.BranchIds);
        var db = scope.ServiceProvider.GetRequiredService<AlmerDbContext>();
        var job = await db.BackgroundJobSet.SingleAsync(x => x.Id == claimed.Id, ct);
        var retry = claimed.Attempts < claimed.MaxAttempts;
        job.Status = retry ? BackgroundJobStatus.Queued : BackgroundJobStatus.Failed; job.Error = SafeError(error); job.LockedAt = null; job.AvailableAt = DateTimeOffset.UtcNow.AddSeconds(JobRetryPolicy.DelaySeconds(claimed.Attempts)); job.CompletedAt = retry ? null : DateTimeOffset.UtcNow;
        if (claimed.Type == BackgroundJobType.ImportCommit)
        {
            var payload = JsonSerializer.Deserialize<ImportCommitPayload>(claimed.PayloadJson, JsonOptions);
            var batch = payload is null ? null : await db.ImportBatchSet.SingleOrDefaultAsync(x => x.Id == payload.ImportBatchId, ct);
            if (batch is not null) { batch.Status = retry ? ImportStatus.Queued : ImportStatus.Failed; batch.FailureMessage = job.Error; }
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task RecoverStaleJobs(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlmerDbContext>();
        var recovered = await db.Database.ExecuteSqlRawAsync("""
            UPDATE "BackgroundJobSet" SET "Status" = 0, "LockedAt" = NULL, "AvailableAt" = now(), "Error" = 'Recovered after worker interruption.'
            WHERE "Status" = 1 AND "LockedAt" < now() - interval '15 minutes'
            """, ct);
        if (recovered > 0) await db.Database.ExecuteSqlRawAsync("""
            UPDATE "ImportBatchSet" AS batch SET "Status" = 4, "FailureMessage" = NULL
            FROM "BackgroundJobSet" AS job
            WHERE batch."BackgroundJobId" = job."Id" AND batch."Status" = 5 AND job."Status" = 0
            """, ct);
        if (recovered > 0) logger.LogWarning("Recovered {Count} stale background jobs.", recovered);
    }

    private async Task RenewLease(Guid jobId, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(2));
        while (await timer.WaitForNextTickAsync(ct))
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AlmerDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"BackgroundJobSet\" SET \"LockedAt\" = now() WHERE \"Id\" = {jobId} AND \"Status\" = 1", ct);
        }
    }

    private async Task EnqueueNotificationRefreshJobs(CancellationToken ct)
    {
        Guid[] tenantIds;
        using (var discovery = scopeFactory.CreateScope())
        {
            var db = discovery.ServiceProvider.GetRequiredService<AlmerDbContext>();
            tenantIds = await db.TenantSet.AsNoTracking().Where(x => x.IsActive).Select(x => x.Id).ToArrayAsync(ct);
        }
        foreach (var tenantId in tenantIds)
        {
            using var scope = scopeFactory.CreateScope();
            var tenant = scope.ServiceProvider.GetRequiredService<TenantContext>(); tenant.SetBackgroundScope(tenantId, Guid.Empty, []);
            var db = scope.ServiceProvider.GetRequiredService<AlmerDbContext>();
            if (await db.BackgroundJobSet.AnyAsync(x => x.Type == BackgroundJobType.NotificationRefresh && (x.Status == BackgroundJobStatus.Queued || x.Status == BackgroundJobStatus.Running), ct)) continue;
            db.BackgroundJobSet.Add(new BackgroundJob { Id = Guid.NewGuid(), TenantId = tenantId, Type = BackgroundJobType.NotificationRefresh, Status = BackgroundJobStatus.Queued, PayloadJson = "{}", BranchIdsJson = "[]", MaxAttempts = 3, CreatedAt = DateTimeOffset.UtcNow, AvailableAt = DateTimeOffset.UtcNow });
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { logger.LogDebug("Notification refresh was already queued for tenant {TenantId}.", tenantId); }
        }
    }

    private static IReadOnlySet<Guid> DeserializeBranches(string json) { try { return (JsonSerializer.Deserialize<Guid[]>(json, JsonOptions) ?? []).ToHashSet(); } catch (JsonException) { return new HashSet<Guid>(); } }
    private static string SafeError(Exception error) => (error is ArgumentException or InvalidOperationException or KeyNotFoundException ? error.Message : "The background job could not be completed.")[..Math.Min(error is ArgumentException or InvalidOperationException or KeyNotFoundException ? error.Message.Length : 42, 500)];
    private sealed record ClaimedJob(Guid Id, Guid TenantId, Guid UserId, IReadOnlySet<Guid> BranchIds, BackgroundJobType Type, string PayloadJson, int Attempts, int MaxAttempts);
}
