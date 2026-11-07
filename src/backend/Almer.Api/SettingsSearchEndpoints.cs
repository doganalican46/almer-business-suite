using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Almer.Application;
using Almer.Domain;
using Almer.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Almer.Api;

public static class SettingsSearchEndpoints
{
    public static readonly string[] ModuleCodes = ["dashboard", "analytics", "sales", "products", "inventory", "pricing", "purchasing", "suppliers", "finance", "reports", "imports", "companies", "roles", "audit"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static RouteGroupBuilder MapSettingsSearchEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/settings", GetSettings);
        group.MapPut("/settings", UpdateSettings).RequirePermission(PermissionCodes.SettingsManage);
        group.MapGet("/dashboard/preferences", GetDashboardPreferences).RequirePermission(PermissionCodes.DashboardRead);
        group.MapPut("/dashboard/preferences", UpdateDashboardPreferences).RequirePermission(PermissionCodes.DashboardRead);
        group.MapGet("/search", Search);
        return group;
    }

    private static async Task<IResult> GetSettings(AlmerDbContext db, CancellationToken ct)
    {
        var value = await db.TenantSettingSet.AsNoTracking().SingleOrDefaultAsync(ct); return Results.Ok(value is null ? Defaults() : Map(value));
    }

    private static async Task<IResult> UpdateSettings(SettingsInput input, ITenantContext tenant, AlmerDbContext db, CancellationToken ct)
    {
        Validate(input); var value = await db.TenantSettingSet.SingleOrDefaultAsync(ct);
        if (value is null) { value = new TenantSetting { Id = Guid.NewGuid(), TenantId = tenant.TenantId }; db.TenantSettingSet.Add(value); }
        value.LogoUrl = Clean(input.LogoUrl); value.PrimaryColor = input.PrimaryColor.ToUpperInvariant(); value.EnabledModulesJson = JsonSerializer.Serialize(input.EnabledModules.Distinct().ToArray(), JsonOptions); value.CriticalStockThreshold = input.CriticalStockThreshold; value.ExpiryWarningDays = input.ExpiryWarningDays;
        await db.SaveChangesAsync(ct); return Results.Ok(Map(value));
    }

    private static async Task<IResult> GetDashboardPreferences(ITenantContext tenant, AlmerDbContext db, CancellationToken ct)
    {
        var value = await db.DashboardPreferenceSet.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == tenant.UserId, ct); return Results.Ok(value is null ? new DashboardPreferenceDto([], []) : Map(value));
    }

    private static async Task<IResult> UpdateDashboardPreferences(DashboardPreferenceInput input, ITenantContext tenant, AlmerDbContext db, CancellationToken ct)
    {
        var allowed = new HashSet<string>(["revenue", "netSales", "grossProfit", "grossMargin", "expenses", "operatingResult", "transactions", "averageBasket", "stockValue", "criticalStock", "trend", "branches"]); if (input.HiddenWidgets.Any(x => !allowed.Contains(x)) || input.WidgetOrder.Any(x => !allowed.Contains(x)) || input.HiddenWidgets.Count > allowed.Count || input.WidgetOrder.Distinct().Count() != input.WidgetOrder.Count) throw new ArgumentException("Invalid dashboard preference.");
        var value = await db.DashboardPreferenceSet.SingleOrDefaultAsync(x => x.UserId == tenant.UserId, ct); if (value is null) { value = new DashboardPreference { Id = Guid.NewGuid(), TenantId = tenant.TenantId, UserId = tenant.UserId }; db.DashboardPreferenceSet.Add(value); }
        value.HiddenWidgetsJson = JsonSerializer.Serialize(input.HiddenWidgets.Distinct().ToArray(), JsonOptions); value.WidgetOrderJson = JsonSerializer.Serialize(input.WidgetOrder, JsonOptions); await db.SaveChangesAsync(ct); return Results.Ok(Map(value));
    }

    private static async Task<IResult> Search(string q, ClaimsPrincipal principal, ITenantContext tenant, AlmerDbContext db, CancellationToken ct)
    {
        q = q.Trim(); if (q.Length < 2) return Results.Ok(Array.Empty<SearchResult>()); if (q.Length > 80) throw new ArgumentException("Search query is too long."); var pattern = $"%{q.Replace("%", "\\%").Replace("_", "\\_")}%"; var results = new List<SearchResult>(); bool Has(string permission) => principal.HasClaim("permission", permission);
        if (Has(PermissionCodes.ProductsRead)) results.AddRange(await db.ProductSet.AsNoTracking().Where(x => EF.Functions.ILike(x.Sku, pattern) || EF.Functions.ILike(x.Name.De, pattern) || EF.Functions.ILike(x.Name.Tr, pattern) || x.Barcode != null && EF.Functions.ILike(x.Barcode, pattern)).OrderBy(x => x.Sku).Take(6).Select(x => new SearchResult("product", x.Name.De, x.Sku, "/products")).ToListAsync(ct));
        if (Has(PermissionCodes.DashboardRead) || Has(PermissionCodes.BranchesManage)) { var branches = db.BranchSet.AsNoTracking().Where(x => EF.Functions.ILike(x.Code, pattern) || EF.Functions.ILike(x.Name.De, pattern) || EF.Functions.ILike(x.Name.Tr, pattern)); if (tenant.AllowedBranchIds.Count > 0) branches = branches.Where(x => tenant.AllowedBranchIds.Contains(x.Id)); results.AddRange(await branches.Take(5).Select(x => new SearchResult("branch", x.Name.De, x.Code, "/branches")).ToListAsync(ct)); }
        if (Has(PermissionCodes.SuppliersManage)) results.AddRange(await db.SupplierSet.AsNoTracking().Where(x => EF.Functions.ILike(x.Code, pattern) || EF.Functions.ILike(x.Name, pattern)).Take(5).Select(x => new SearchResult("supplier", x.Name, x.Code, "/suppliers")).ToListAsync(ct));
        if (Has(PermissionCodes.SalesRead)) { var sales = db.SaleSet.AsNoTracking().Where(x => EF.Functions.ILike(x.TransactionNumber, pattern)); if (tenant.AllowedBranchIds.Count > 0) sales = sales.Where(x => tenant.AllowedBranchIds.Contains(x.BranchId)); results.AddRange(await sales.OrderByDescending(x => x.OccurredAt).Take(5).Select(x => new SearchResult("sale", x.TransactionNumber, x.PaymentMethod, "/sales")).ToListAsync(ct)); }
        return Results.Ok(results.Take(15));
    }

    private static SettingsDto Defaults() => new(null, "#147A46", ModuleCodes, 10, 14);
    private static SettingsDto Map(TenantSetting x) => new(x.LogoUrl, x.PrimaryColor, JsonSerializer.Deserialize<string[]>(x.EnabledModulesJson, JsonOptions) ?? ModuleCodes, x.CriticalStockThreshold, x.ExpiryWarningDays);
    private static DashboardPreferenceDto Map(DashboardPreference x) => new(JsonSerializer.Deserialize<string[]>(x.HiddenWidgetsJson, JsonOptions) ?? [], JsonSerializer.Deserialize<string[]>(x.WidgetOrderJson, JsonOptions) ?? []);
    private static void Validate(SettingsInput x) { if (!Regex.IsMatch(x.PrimaryColor, "^#[0-9A-Fa-f]{6}$") || x.EnabledModules.Any(m => !ModuleCodes.Contains(m)) || x.CriticalStockThreshold is < 0 or > 100_000 || x.ExpiryWarningDays is < 0 or > 365) throw new ArgumentException("Invalid settings."); var logo = Clean(x.LogoUrl); if (logo is not null && !logo.StartsWith('/') && (!Uri.TryCreate(logo, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)) throw new ArgumentException("Logo URL must be relative or HTTPS."); }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record SettingsDto(string? LogoUrl, string PrimaryColor, IReadOnlyList<string> EnabledModules, int CriticalStockThreshold, int ExpiryWarningDays);
public sealed record SettingsInput(string? LogoUrl, string PrimaryColor, IReadOnlyList<string> EnabledModules, int CriticalStockThreshold, int ExpiryWarningDays);
public sealed record DashboardPreferenceDto(IReadOnlyList<string> HiddenWidgets, IReadOnlyList<string> WidgetOrder);
public sealed record DashboardPreferenceInput(IReadOnlyList<string> HiddenWidgets, IReadOnlyList<string> WidgetOrder);
public sealed record SearchResult(string Type, string Title, string Subtitle, string Url);
