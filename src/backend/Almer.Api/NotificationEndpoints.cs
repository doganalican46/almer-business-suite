using System.Security.Claims;
using System.Text.Json;
using Almer.Application;
using Almer.Domain;
using Almer.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Almer.Api;

public static class NotificationEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public static RouteGroupBuilder MapNotificationEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/notifications", List).RequirePermission(PermissionCodes.DashboardRead);
        group.MapPut("/notifications/{id:guid}/read", MarkRead).RequirePermission(PermissionCodes.DashboardRead);
        group.MapPut("/notifications/read-all", MarkAllRead).RequirePermission(PermissionCodes.DashboardRead);
        return group;
    }

    private static async Task<IResult> List(string? locale, ITenantContext tenant, AlmerDbContext db, CancellationToken ct)
    {
        var query = db.NotificationSet.AsNoTracking().Where(x => x.ResolvedAt == null); if (tenant.AllowedBranchIds.Count > 0) query = query.Where(x => x.BranchId.HasValue && tenant.AllowedBranchIds.Contains(x.BranchId.Value));
        var rows = await query.OrderByDescending(x => x.Severity).ThenByDescending(x => x.UpdatedAt).Take(50).ToListAsync(ct); var ids = rows.Select(x => x.Id).ToArray(); var read = await db.NotificationReadSet.AsNoTracking().Where(x => x.UserId == tenant.UserId && ids.Contains(x.NotificationId)).Select(x => x.NotificationId).ToHashSetAsync(ct); var language = locale == "tr-TR" ? "tr-TR" : "de-DE";
        return Results.Ok(new { unreadCount = rows.Count(x => !read.Contains(x.Id)), items = rows.Select(x => new { x.Id, x.Type, x.Severity, title = x.Title.For(language), message = x.Message.For(language), x.TargetUrl, x.BranchId, x.ProductId, x.UpdatedAt, isRead = read.Contains(x.Id) }) });
    }

    private static async Task<IResult> MarkRead(Guid id, ITenantContext tenant, AlmerDbContext db, CancellationToken ct)
    {
        var notification = await db.NotificationSet.SingleOrDefaultAsync(x => x.Id == id && x.ResolvedAt == null, ct) ?? throw new KeyNotFoundException("Notification not found."); EnsureBranch(tenant, notification.BranchId); if (!await db.NotificationReadSet.AnyAsync(x => x.NotificationId == id && x.UserId == tenant.UserId, ct)) db.NotificationReadSet.Add(new NotificationRead { TenantId = tenant.TenantId, NotificationId = id, UserId = tenant.UserId, ReadAt = DateTimeOffset.UtcNow }); await db.SaveChangesAsync(ct); return Results.NoContent();
    }

    private static async Task<IResult> MarkAllRead(ITenantContext tenant, AlmerDbContext db, CancellationToken ct)
    {
        var query = db.NotificationSet.Where(x => x.ResolvedAt == null); if (tenant.AllowedBranchIds.Count > 0) query = query.Where(x => x.BranchId.HasValue && tenant.AllowedBranchIds.Contains(x.BranchId.Value)); var ids = await query.Select(x => x.Id).ToListAsync(ct); var existing = await db.NotificationReadSet.Where(x => x.UserId == tenant.UserId && ids.Contains(x.NotificationId)).Select(x => x.NotificationId).ToListAsync(ct); db.NotificationReadSet.AddRange(ids.Except(existing).Select(id => new NotificationRead { TenantId = tenant.TenantId, NotificationId = id, UserId = tenant.UserId, ReadAt = DateTimeOffset.UtcNow })); await db.SaveChangesAsync(ct); return Results.NoContent();
    }

    public static async Task RefreshAsync(ITenantContext tenant, AlmerDbContext db, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct); await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({tenant.TenantId.ToString()}))", ct);
        var now = DateTimeOffset.UtcNow; var today = DateOnly.FromDateTime(now.UtcDateTime); var settings = await db.TenantSettingSet.AsNoTracking().SingleOrDefaultAsync(ct); var threshold = settings?.CriticalStockThreshold ?? 10; var expiryDays = settings?.ExpiryWarningDays ?? 14;
        var branches = db.BranchSet.AsNoTracking().Where(x => x.IsActive); if (tenant.AllowedBranchIds.Count > 0) branches = branches.Where(x => tenant.AllowedBranchIds.Contains(x.Id)); var branchRows = await branches.ToListAsync(ct); var branchIds = branchRows.Select(x => x.Id).ToArray(); var products = await db.ProductSet.AsNoTracking().Where(x => x.IsActive).ToListAsync(ct); var movements = await db.StockMovementSet.AsNoTracking().Where(x => branchIds.Contains(x.BranchId)).ToListAsync(ct); var balances = movements.GroupBy(x => new { x.BranchId, x.ProductId }).ToDictionary(x => x.Key, x => x.Sum(y => y.QuantityDelta)); var candidates = new Dictionary<string, Candidate>();
        foreach (var branch in branchRows) foreach (var product in products) { var balance = balances.GetValueOrDefault(new { BranchId = branch.Id, ProductId = product.Id }); if (!NotificationRules.IsCritical(balance, product.MinimumStock, threshold)) continue; var key = $"critical:{branch.Id}:{product.Id}"; candidates[key] = new(branch.Id, product.Id, NotificationType.CriticalStock, NotificationSeverity.Critical, "Kritischer Lagerbestand", "Kritik stok seviyesi", $"{product.Name.De}: {balance:0.###} in {branch.Name.De}", $"{product.Name.Tr}: {branch.Name.Tr} şubesinde {balance:0.###}", "/inventory"); }
        foreach (var movement in movements.Where(x => x.QuantityDelta > 0 && NotificationRules.IsExpiring(x.ExpiresOn, today, expiryDays))) { var product = products.FirstOrDefault(x => x.Id == movement.ProductId); var branch = branchRows.FirstOrDefault(x => x.Id == movement.BranchId); if (product is null || branch is null) continue; var key = $"expiry:{movement.Id}"; candidates[key] = new(branch.Id, product.Id, NotificationType.ExpiringStock, NotificationSeverity.Warning, "Mindesthaltbarkeit beachten", "Son kullanma tarihine dikkat", $"{product.Name.De} ({movement.LotNumber ?? "-"}) läuft am {movement.ExpiresOn:dd.MM.yyyy} ab.", $"{product.Name.Tr} ({movement.LotNumber ?? "-"}) {movement.ExpiresOn:dd.MM.yyyy} tarihinde sona eriyor.", "/inventory"); }
        var existing = await db.NotificationSet.Where(x => (x.ExternalKey.StartsWith("critical:") || x.ExternalKey.StartsWith("expiry:")) && (x.BranchId == null || branchIds.Contains(x.BranchId.Value))).ToListAsync(ct); var changed = false; var changedNotifications = new List<Notification>();
        foreach (var candidate in candidates) { var row = existing.FirstOrDefault(x => x.ExternalKey == candidate.Key); if (row is null) { var c = candidate.Value; row = new Notification { Id = Guid.NewGuid(), TenantId = tenant.TenantId, BranchId = c.BranchId, ProductId = c.ProductId, ExternalKey = candidate.Key, Type = c.Type, Severity = c.Severity, Title = new(c.TitleDe, c.TitleTr), Message = new(c.MessageDe, c.MessageTr), TargetUrl = c.TargetUrl, CreatedAt = now, UpdatedAt = now }; db.NotificationSet.Add(row); changedNotifications.Add(row); changed = true; } else { var c = candidate.Value; if (row.ResolvedAt.HasValue || row.Message.De != c.MessageDe || row.Message.Tr != c.MessageTr) { row.ResolvedAt = null; row.Message = new(c.MessageDe, c.MessageTr); row.UpdatedAt = now; changedNotifications.Add(row); changed = true; } } }
        foreach (var stale in existing.Where(x => x.ResolvedAt == null && !candidates.ContainsKey(x.ExternalKey))) { stale.ResolvedAt = now; stale.UpdatedAt = now; changed = true; }
        if (changed)
        {
            var channels = await db.NotificationChannelSettingSet.AsNoTracking().SingleOrDefaultAsync(ct);
            if (channels is not null) foreach (var notification in changedNotifications) { if (channels.EmailEnabled) QueueDelivery(notification.Id, BackgroundJobType.NotificationEmailDelivery, tenant.TenantId, now, db); if (channels.WebhookEnabled) QueueDelivery(notification.Id, BackgroundJobType.NotificationWebhookDelivery, tenant.TenantId, now, db); }
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }

    private static void EnsureBranch(ITenantContext tenant, Guid? branchId) { if (tenant.AllowedBranchIds.Count > 0 && (!branchId.HasValue || !tenant.AllowedBranchIds.Contains(branchId.Value))) throw new UnauthorizedAccessException("Branch access denied."); }
    private static void QueueDelivery(Guid notificationId, BackgroundJobType type, Guid tenantId, DateTimeOffset now, AlmerDbContext db) => db.BackgroundJobSet.Add(new BackgroundJob { Id = Guid.NewGuid(), TenantId = tenantId, Type = type, Status = BackgroundJobStatus.Queued, PayloadJson = JsonSerializer.Serialize(new NotificationDeliveryPayload(notificationId, Guid.NewGuid()), JsonOptions), BranchIdsJson = "[]", MaxAttempts = 5, CreatedAt = now, AvailableAt = now });
    private sealed record Candidate(Guid BranchId, Guid ProductId, NotificationType Type, NotificationSeverity Severity, string TitleDe, string TitleTr, string MessageDe, string MessageTr, string TargetUrl);
}
