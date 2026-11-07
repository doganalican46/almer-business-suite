using Almer.Application;
using Almer.Domain;
using Almer.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Almer.Api;

public static class ReportEndpoints
{
    public static RouteGroupBuilder MapReportEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/reports/overview", Overview).RequirePermission(PermissionCodes.ReportsRead);
        group.MapGet("/reports/tax-summary", TaxSummaryEndpoint).RequireAnyPermission(PermissionCodes.ReportsRead, PermissionCodes.FinanceRead);
        group.MapGet("/reports/export", Export).RequirePermission(PermissionCodes.ReportsExport);
        return group;
    }

    private static async Task<IResult> Overview(DateOnly? from, DateOnly? to, Guid? branchId, string? locale, ITenantContext tenant, AlmerDbContext db, CancellationToken ct)
    {
        var period = Period(from, to); EnsureBranch(tenant, branchId); var data = await Load(period.From, period.To, branchId, tenant, db, ct); var language = Locale(locale);
        var lines = data.Sales.SelectMany(x => x.Lines).ToArray(); var summary = ProfitabilityCalculator.Calculate(lines, data.Entries);
        var branchNames = data.Branches.ToDictionary(x => x.Id, x => x.Name.For(language)); var productMap = data.Products.ToDictionary(x => x.Id); var categoryMap = data.Categories.ToDictionary(x => x.Id, x => x.Name.For(language)); var financialNames = data.FinancialCategories.ToDictionary(x => x.Id, x => x.Name.For(language));
        var daily = data.Sales.GroupBy(x => DateOnly.FromDateTime(x.OccurredAt.Date)).OrderBy(x => x.Key).Select(x => new { date = x.Key, netSales = x.Sum(y => y.NetAmount), grossSales = x.Sum(y => y.GrossAmount), transactions = x.Count() });
        var byBranch = data.Sales.GroupBy(x => x.BranchId).Select(x => new { branchId = x.Key, name = branchNames.GetValueOrDefault(x.Key, "-"), netSales = x.Sum(y => y.NetAmount), grossProfit = x.SelectMany(y => y.Lines).Sum(y => y.NetAmount - y.Quantity * y.UnitCost), transactions = x.Count() }).OrderByDescending(x => x.netSales);
        var byCategory = lines.GroupBy(x => productMap.GetValueOrDefault(x.ProductId)?.CategoryId).Select(x => new { categoryId = x.Key, name = x.Key.HasValue ? categoryMap.GetValueOrDefault(x.Key.Value, "-") : "-", netSales = x.Sum(y => y.NetAmount), grossProfit = x.Sum(y => y.NetAmount - y.Quantity * y.UnitCost) }).OrderByDescending(x => x.netSales);
        var expenses = data.Entries.Where(x => x.Type == FinancialEntryType.Expense).GroupBy(x => x.CategoryId).Select(x => new { categoryId = x.Key, name = financialNames.GetValueOrDefault(x.Key, "-"), amount = x.Sum(y => y.NetAmount) }).OrderByDescending(x => x.amount);
        var products = lines.GroupBy(x => new { x.ProductId, x.ProductNameSnapshot }).Select(x => new { x.Key.ProductId, name = x.Key.ProductNameSnapshot, netSales = x.Sum(y => y.NetAmount), grossProfit = x.Sum(y => y.NetAmount - y.Quantity * y.UnitCost), quantity = x.Sum(y => y.Quantity), margin = x.Sum(y => y.NetAmount) == 0 ? 0 : decimal.Round(x.Sum(y => y.NetAmount - y.Quantity * y.UnitCost) / x.Sum(y => y.NetAmount) * 100, 2) }).OrderByDescending(x => x.grossProfit);
        return Results.Ok(new { period.From, period.To, summary, daily, byBranch, byCategory, expenses, topProducts = products.Take(10), lowMarginProducts = products.OrderBy(x => x.margin).Take(10), generatedAt = DateTimeOffset.UtcNow });
    }

    private static async Task<IResult> TaxSummaryEndpoint(DateOnly? from, DateOnly? to, Guid? branchId, ITenantContext tenant, AlmerDbContext db, CancellationToken ct)
    {
        var period = Period(from, to); EnsureBranch(tenant, branchId); return Results.Ok(await BuildTaxSummary(period.From, period.To, branchId, tenant, db, ct));
    }

    private static async Task<IResult> Export(string report, string format, DateOnly? from, DateOnly? to, Guid? branchId, string? locale, ITenantContext tenant, AlmerDbContext db, CancellationToken ct)
    {
        var period = Period(from, to); EnsureBranch(tenant, branchId); var language = Locale(locale); var dataset = await BuildDataset(report, period.From, period.To, branchId, language, tenant, db, ct); var exported = ReportExporter.Export(dataset, format); var safeName = report.ToLowerInvariant() is "sales" or "purchases" or "expenses" or "inventory" or "profitability" or "tax" ? report.ToLowerInvariant() : "report";
        return Results.File(exported.Content, exported.ContentType, $"almer-{safeName}-{period.From:yyyyMMdd}-{period.To:yyyyMMdd}.{exported.Extension}");
    }

    private static async Task<ReportDataset> BuildDataset(string report, DateOnly from, DateOnly to, Guid? branchId, string locale, ITenantContext tenant, AlmerDbContext db, CancellationToken ct)
    {
        var data = await Load(from, to, branchId, tenant, db, ct); var tr = locale == "tr-TR"; var subtitle = $"{from:yyyy-MM-dd} - {to:yyyy-MM-dd}"; var branches = data.Branches.ToDictionary(x => x.Id, x => x.Name.For(locale));
        if (report.Equals("sales", StringComparison.OrdinalIgnoreCase)) return new(tr ? "Satış Raporu" : "Verkaufsbericht", subtitle, tr ? ["İşlem", "Tarih", "Şube", "Ödeme", "Net", "Vergi", "Brüt", "İndirim", "Kaynak"] : ["Transaktion", "Datum", "Filiale", "Zahlung", "Netto", "Steuer", "Brutto", "Rabatt", "Quelle"], data.Sales.OrderByDescending(x => x.OccurredAt).Take(10_000).Select(x => Row(x.TransactionNumber, x.OccurredAt.ToString("yyyy-MM-dd HH:mm"), branches.GetValueOrDefault(x.BranchId, "-"), x.PaymentMethod, Money(x.NetAmount), Money(x.TaxAmount), Money(x.GrossAmount), Money(x.DiscountAmount), x.SourceSystem)).ToArray());
        if (report.Equals("purchases", StringComparison.OrdinalIgnoreCase)) { var suppliers = await db.SupplierSet.ToDictionaryAsync(x => x.Id, x => x.Name, ct); return new(tr ? "Satın Alma Raporu" : "Einkaufsbericht", subtitle, tr ? ["Belge", "Tarih", "Şube", "Tedarikçi", "Net", "Vergi", "Brüt"] : ["Beleg", "Datum", "Filiale", "Lieferant", "Netto", "Steuer", "Brutto"], data.Purchases.OrderByDescending(x => x.Date).Take(10_000).Select(x => Row(x.DocumentNumber, x.Date.ToString("yyyy-MM-dd"), branches.GetValueOrDefault(x.BranchId, "-"), suppliers.GetValueOrDefault(x.SupplierId, "-"), Money(x.NetAmount), Money(x.TaxAmount), Money(x.GrossAmount))).ToArray()); }
        if (report.Equals("expenses", StringComparison.OrdinalIgnoreCase)) { var categories = data.FinancialCategories.ToDictionary(x => x.Id, x => x.Name.For(locale)); return new(tr ? "Gider Raporu" : "Ausgabenbericht", subtitle, tr ? ["Tarih", "Şube", "Kategori", "Açıklama", "Net", "Vergi", "Brüt", "Onay"] : ["Datum", "Filiale", "Kategorie", "Beschreibung", "Netto", "Steuer", "Brutto", "Freigabe"], data.Entries.Where(x => x.Type == FinancialEntryType.Expense).OrderByDescending(x => x.Date).Take(10_000).Select(x => Row(x.Date.ToString("yyyy-MM-dd"), x.BranchId.HasValue ? branches.GetValueOrDefault(x.BranchId.Value, "-") : tr ? "Merkez" : "Zentrale", categories.GetValueOrDefault(x.CategoryId, "-"), x.Description, Money(x.NetAmount), Money(x.TaxAmount), Money(x.GrossAmount), x.IsApproved ? tr ? "Onaylı" : "Freigegeben" : tr ? "Bekliyor" : "Ausstehend")).ToArray()); }
        if (report.Equals("inventory", StringComparison.OrdinalIgnoreCase)) { var movements = db.StockMovementSet.AsQueryable(); if (branchId.HasValue) movements = movements.Where(x => x.BranchId == branchId); if (tenant.AllowedBranchIds.Count > 0) movements = movements.Where(x => tenant.AllowedBranchIds.Contains(x.BranchId)); var balances = await movements.GroupBy(x => new { x.BranchId, x.ProductId }).Select(x => new { x.Key.BranchId, x.Key.ProductId, Quantity = x.Sum(y => y.QuantityDelta) }).ToListAsync(ct); var products = data.Products.ToDictionary(x => x.Id); return new(tr ? "Stok Raporu" : "Lagerbericht", DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'"), tr ? ["Şube", "SKU", "Ürün", "Miktar", "Birim maliyet", "Stok değeri"] : ["Filiale", "SKU", "Produkt", "Bestand", "Stückkosten", "Lagerwert"], balances.Take(10_000).Where(x => products.ContainsKey(x.ProductId)).Select(x => { var p = products[x.ProductId]; return Row(branches.GetValueOrDefault(x.BranchId, "-"), p.Sku, p.Name.For(locale), Number(x.Quantity), Money(p.PurchaseCost), Money(x.Quantity * p.PurchaseCost)); }).ToArray()); }
        if (report.Equals("profitability", StringComparison.OrdinalIgnoreCase)) { var lines = data.Sales.SelectMany(x => x.Lines); return new(tr ? "Ürün Kârlılığı" : "Produktrentabilität", subtitle, tr ? ["Ürün", "Miktar", "Net satış", "Maliyet", "Brüt kâr", "Marj %"] : ["Produkt", "Menge", "Nettoumsatz", "Kosten", "Bruttogewinn", "Marge %"], lines.GroupBy(x => new { x.ProductId, x.ProductNameSnapshot }).Select(x => { var net = x.Sum(y => y.NetAmount); var cost = x.Sum(y => y.Quantity * y.UnitCost); return Row(x.Key.ProductNameSnapshot, Number(x.Sum(y => y.Quantity)), Money(net), Money(cost), Money(net - cost), Number(net == 0 ? 0 : (net - cost) / net * 100)); }).OrderByDescending(x => decimal.Parse(x[4], System.Globalization.CultureInfo.InvariantCulture)).Take(10_000).ToArray()); }
        if (report.Equals("tax", StringComparison.OrdinalIgnoreCase)) { var tax = await BuildTaxSummary(from, to, branchId, tenant, db, ct); return new(tr ? "Vergi Özeti" : "Steuerübersicht", subtitle, tr ? ["Vergi oranı %", "Satış net", "Hesaplanan vergi", "Alış vergisi", "Gider vergisi", "Net vergi"] : ["Steuersatz %", "Verkauf netto", "Umsatzsteuer", "Vorsteuer Einkauf", "Vorsteuer Aufwand", "Nettosteuer"], tax.Rates.Select(x => Row(Number(x.Rate), Money(x.SalesNet), Money(x.OutputTax), Money(x.PurchaseInputTax), Money(x.ExpenseInputTax), Money(x.NetTax))).ToArray()); }
        throw new ArgumentException("Unknown report type.");
    }

    private static async Task<TaxSummaryResult> BuildTaxSummary(DateOnly from, DateOnly to, Guid? branchId, ITenantContext tenant, AlmerDbContext db, CancellationToken ct)
    {
        var data = await Load(from, to, branchId, tenant, db, ct); var rates = TaxSummaryCalculator.Calculate(data.Sales.SelectMany(x => x.Lines), data.Purchases.SelectMany(x => x.Lines), data.Entries).Select(x => new TaxRateSummary(x.Rate, x.SalesNet, x.OutputTax, x.PurchaseInputTax, x.ExpenseInputTax, x.NetTax)).ToArray();
        return new(from, to, rates, rates.Sum(x => x.OutputTax), rates.Sum(x => x.PurchaseInputTax + x.ExpenseInputTax), rates.Sum(x => x.NetTax), false, "Management estimate only. Verify tax classification and official filing requirements with a tax professional.");
    }

    private static async Task<ReportData> Load(DateOnly from, DateOnly to, Guid? branchId, ITenantContext tenant, AlmerDbContext db, CancellationToken ct)
    {
        var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero); var end = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var sales = db.SaleSet.Include(x => x.Lines).Where(x => x.OccurredAt >= start && x.OccurredAt < end); var purchases = db.PurchaseSet.Include(x => x.Lines).Where(x => x.Date >= from && x.Date <= to); var entries = db.FinancialEntrySet.Where(x => x.Date >= from && x.Date <= to);
        if (branchId.HasValue) { sales = sales.Where(x => x.BranchId == branchId); purchases = purchases.Where(x => x.BranchId == branchId); entries = entries.Where(x => x.BranchId == branchId); }
        if (tenant.AllowedBranchIds.Count > 0) { sales = sales.Where(x => tenant.AllowedBranchIds.Contains(x.BranchId)); purchases = purchases.Where(x => tenant.AllowedBranchIds.Contains(x.BranchId)); entries = entries.Where(x => x.BranchId.HasValue && tenant.AllowedBranchIds.Contains(x.BranchId.Value)); }
        return new(await sales.AsNoTracking().ToListAsync(ct), await purchases.AsNoTracking().ToListAsync(ct), await entries.AsNoTracking().ToListAsync(ct), await db.BranchSet.AsNoTracking().ToListAsync(ct), await db.ProductSet.AsNoTracking().ToListAsync(ct), await db.ProductCategorySet.AsNoTracking().ToListAsync(ct), await db.FinancialCategorySet.AsNoTracking().ToListAsync(ct));
    }

    private static (DateOnly From, DateOnly To) Period(DateOnly? from, DateOnly? to) { var end = to ?? DateOnly.FromDateTime(DateTime.UtcNow); var start = from ?? end.AddDays(-29); if (end < start || end.DayNumber - start.DayNumber > 370) throw new ArgumentException("Invalid report period."); return (start, end); }
    private static void EnsureBranch(ITenantContext tenant, Guid? branchId) { if (branchId.HasValue && tenant.AllowedBranchIds.Count > 0 && !tenant.AllowedBranchIds.Contains(branchId.Value)) throw new UnauthorizedAccessException("Branch access denied."); }
    private static string Locale(string? locale) => locale == "tr-TR" ? "tr-TR" : "de-DE";
    private static IReadOnlyList<string> Row(params string[] cells) => cells;
    private static string Money(decimal value) => value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
    private static string Number(decimal value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    private sealed record ReportData(List<Sale> Sales, List<Purchase> Purchases, List<FinancialEntry> Entries, List<Branch> Branches, List<Product> Products, List<ProductCategory> Categories, List<FinancialCategory> FinancialCategories);
}

public sealed record TaxRateSummary(decimal Rate, decimal SalesNet, decimal OutputTax, decimal PurchaseInputTax, decimal ExpenseInputTax, decimal NetTax);
public sealed record TaxSummaryResult(DateOnly From, DateOnly To, IReadOnlyList<TaxRateSummary> Rates, decimal OutputTax, decimal InputTax, decimal NetTaxPayable, bool IsOfficialFiling, string Disclaimer);
