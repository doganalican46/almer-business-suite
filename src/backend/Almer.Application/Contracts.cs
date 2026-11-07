using Almer.Domain;

namespace Almer.Application;

public interface ITenantContext
{
    Guid TenantId { get; }
    Guid UserId { get; }
    IReadOnlySet<Guid> AllowedBranchIds { get; }
}

public interface IAlmerData
{
    IQueryable<Tenant> Tenants { get; }
    IQueryable<LegalEntity> LegalEntities { get; }
    IQueryable<Branch> Branches { get; }
    IQueryable<DailyMetric> DailyMetrics { get; }
    IQueryable<Role> Roles { get; }
    IQueryable<User> Users { get; }
    IQueryable<Product> Products { get; }
    IQueryable<StockMovement> StockMovements { get; }
    IQueryable<TenantSetting> TenantSettings { get; }
    void Add<T>(T entity) where T : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed record DashboardFilter(DateOnly From, DateOnly To, Guid? BranchId);
public sealed record KpiValue(string Code, decimal Value, decimal? Change = null);
public sealed record ChartPoint(DateOnly Date, decimal Revenue, decimal Expenses);
public sealed record BranchPerformance(Guid BranchId, string Name, decimal Revenue, decimal GrossProfit);
public sealed record DataQuality(decimal CostMatchRate, int MissingCostLines, bool IsComplete);
public sealed record DashboardResult(
    IReadOnlyList<KpiValue> Kpis,
    IReadOnlyList<ChartPoint> Trend,
    IReadOnlyList<BranchPerformance> Branches,
    DataQuality DataQuality,
    bool IsDemo,
    string Currency,
    DateTimeOffset GeneratedAt);

public sealed record BranchInput(Guid LegalEntityId, string NameDe, string NameTr, string Code, string Address, string? ManagerName, string? Phone, DateOnly? OpenedOn);
public sealed record BranchDto(Guid Id, Guid LegalEntityId, string NameDe, string NameTr, string Code, string Address, string? ManagerName, string? Phone, DateOnly? OpenedOn, bool IsActive);
public sealed record BranchUpdate(Guid LegalEntityId, string NameDe, string NameTr, string Code, string Address, string? ManagerName, string? Phone, DateOnly? OpenedOn, bool IsActive);

public static class FinancialCalculator
{
    public static (decimal NetSales, decimal GrossProfit, decimal Margin, decimal OperatingResult) Calculate(IEnumerable<DailyMetric> source)
    {
        var rows = source.ToArray();
        var netSales = rows.Sum(x => x.GrossSales - x.Vat - x.Returns - x.Discounts);
        var grossProfit = netSales - rows.Sum(x => x.CostOfGoodsSold);
        var margin = netSales == 0 ? 0 : decimal.Round(grossProfit / netSales * 100m, 2);
        return (netSales, grossProfit, margin, grossProfit - rows.Sum(x => x.OperatingExpenses));
    }
}

public sealed class DashboardService(IAlmerData data, ITenantContext tenant)
{
    public async Task<DashboardResult> GetAsync(DashboardFilter filter, string locale, CancellationToken ct)
    {
        if (filter.To < filter.From || filter.To.DayNumber - filter.From.DayNumber > 370) throw new ArgumentException("Invalid date range.");
        if (filter.BranchId.HasValue && tenant.AllowedBranchIds.Count > 0 && !tenant.AllowedBranchIds.Contains(filter.BranchId.Value)) throw new UnauthorizedAccessException("Branch access denied.");

        var query = data.DailyMetrics.Where(x => x.Date >= filter.From && x.Date <= filter.To);
        if (tenant.AllowedBranchIds.Count > 0) query = query.Where(x => tenant.AllowedBranchIds.Contains(x.BranchId));
        if (filter.BranchId.HasValue) query = query.Where(x => x.BranchId == filter.BranchId.Value);
        var rows = await QueryAsync(query, ct);
        var tenantInfo = await SingleAsync(data.Tenants.Where(x => x.Id == tenant.TenantId), ct);
        var branchQuery = data.Branches.Where(x => !filter.BranchId.HasValue || x.Id == filter.BranchId);
        if (tenant.AllowedBranchIds.Count > 0) branchQuery = branchQuery.Where(x => tenant.AllowedBranchIds.Contains(x.Id));
        var branches = await QueryAsync(branchQuery, ct);
        var calc = FinancialCalculator.Calculate(rows);
        var totalGross = rows.Sum(x => x.GrossSales);
        var expenses = rows.Sum(x => x.OperatingExpenses);
        var transactions = rows.Sum(x => x.TransactionCount);
        var lines = rows.Sum(x => x.SalesLines);
        var matched = rows.Sum(x => x.CostMatchedLines);
        var productRows = await QueryAsync(data.Products.Where(x => x.IsActive), ct);
        var stockQuery = data.StockMovements;
        if (filter.BranchId.HasValue) stockQuery = stockQuery.Where(x => x.BranchId == filter.BranchId.Value);
        if (tenant.AllowedBranchIds.Count > 0) stockQuery = stockQuery.Where(x => tenant.AllowedBranchIds.Contains(x.BranchId));
        var stockRows = await QueryAsync(stockQuery, ct);
        var balances = stockRows.GroupBy(x => new { x.BranchId, x.ProductId }).ToDictionary(x => x.Key, x => x.Sum(y => y.QuantityDelta));
        var stockValue = balances.Sum(x => Math.Max(x.Value, 0m) * (productRows.FirstOrDefault(p => p.Id == x.Key.ProductId)?.PurchaseCost ?? 0m));
        var defaultCriticalThreshold = (await QueryAsync(data.TenantSettings, ct)).SingleOrDefault()?.CriticalStockThreshold ?? 10;
        var criticalStock = branches.SelectMany(branch => productRows.Select(product => new { branch.Id, Product = product })).Count(x => (balances.GetValueOrDefault(new { BranchId = x.Id, ProductId = x.Product.Id })) <= (x.Product.MinimumStock > 0 ? x.Product.MinimumStock : defaultCriticalThreshold));

        return new DashboardResult(
            [new("revenue", totalGross), new("netSales", calc.NetSales), new("grossProfit", calc.GrossProfit), new("grossMargin", calc.Margin), new("expenses", expenses), new("operatingResult", calc.OperatingResult), new("transactions", transactions), new("averageBasket", transactions == 0 ? 0 : decimal.Round(totalGross / transactions, 2)), new("stockValue", stockValue), new("criticalStock", criticalStock)],
            rows.GroupBy(x => x.Date).OrderBy(x => x.Key).Select(g => new ChartPoint(g.Key, g.Sum(x => x.GrossSales), g.Sum(x => x.OperatingExpenses))).ToArray(),
            branches.Select(branch => { var branchRows = rows.Where(x => x.BranchId == branch.Id); var c = FinancialCalculator.Calculate(branchRows); return new BranchPerformance(branch.Id, branch.Name.For(locale), branchRows.Sum(x => x.GrossSales), c.GrossProfit); }).ToArray(),
            new(lines == 0 ? 0 : decimal.Round(matched * 100m / lines, 1), lines - matched, lines > 0 && lines == matched),
            tenantInfo.IsDemo, tenantInfo.Currency, DateTimeOffset.UtcNow);
    }

    private static async Task<List<T>> QueryAsync<T>(IQueryable<T> query, CancellationToken ct) => await Task.Run(() => query.ToList(), ct);
    private static async Task<T> SingleAsync<T>(IQueryable<T> query, CancellationToken ct) => await Task.Run(query.Single, ct);
}

public sealed class BranchService(IAlmerData data, ITenantContext tenant)
{
    public async Task<IReadOnlyList<BranchDto>> ListAsync(CancellationToken ct)
    {
        var query = data.Branches;
        if (tenant.AllowedBranchIds.Count > 0) query = query.Where(x => tenant.AllowedBranchIds.Contains(x.Id));
        return (await Task.Run(() => query.OrderBy(x => x.Code).ToList(), ct)).Select(Map).ToArray();
    }

    public async Task<BranchDto> CreateAsync(BranchInput input, CancellationToken ct)
    {
        if (tenant.AllowedBranchIds.Count > 0) throw new UnauthorizedAccessException("Branch-scoped users cannot create company-wide branches.");
        if (string.IsNullOrWhiteSpace(input.NameDe) || string.IsNullOrWhiteSpace(input.Code)) throw new ArgumentException("Name and code are required.");
        if (!data.LegalEntities.Any(x => x.Id == input.LegalEntityId)) throw new ArgumentException("Legal entity not found in this tenant.");
        if (data.Branches.Any(x => x.Code == input.Code.Trim())) throw new ArgumentException("Branch code already exists.");
        var branch = new Branch { Id = Guid.NewGuid(), TenantId = tenant.TenantId, LegalEntityId = input.LegalEntityId, Name = new(input.NameDe, input.NameTr), Code = input.Code.Trim().ToUpperInvariant(), Address = input.Address.Trim(), ManagerName = input.ManagerName, Phone = input.Phone, OpenedOn = input.OpenedOn };
        data.Add(branch); await data.SaveChangesAsync(ct); return Map(branch);
    }
    public async Task<BranchDto> UpdateAsync(Guid id, BranchUpdate input, CancellationToken ct)
    {
        var branch = data.Branches.SingleOrDefault(x => x.Id == id) ?? throw new KeyNotFoundException("Branch not found.");
        EnsureBranchAccess(id);
        if (!data.LegalEntities.Any(x => x.Id == input.LegalEntityId)) throw new ArgumentException("Legal entity not found in this tenant.");
        if (string.IsNullOrWhiteSpace(input.NameDe) || string.IsNullOrWhiteSpace(input.Code)) throw new ArgumentException("Name and code are required.");
        if (data.Branches.Any(x => x.Id != id && x.Code == input.Code.Trim().ToUpperInvariant())) throw new ArgumentException("Branch code already exists.");
        branch.LegalEntityId = input.LegalEntityId; branch.Name = new(input.NameDe, input.NameTr); branch.Code = input.Code.Trim().ToUpperInvariant();
        branch.Address = input.Address.Trim(); branch.ManagerName = input.ManagerName; branch.Phone = input.Phone; branch.OpenedOn = input.OpenedOn; branch.IsActive = input.IsActive;
        await data.SaveChangesAsync(ct); return Map(branch);
    }
    public async Task DeactivateAsync(Guid id, CancellationToken ct)
    {
        var branch = data.Branches.SingleOrDefault(x => x.Id == id) ?? throw new KeyNotFoundException("Branch not found.");
        EnsureBranchAccess(id);
        branch.IsActive = false; await data.SaveChangesAsync(ct);
    }
    private void EnsureBranchAccess(Guid id) { if (tenant.AllowedBranchIds.Count > 0 && !tenant.AllowedBranchIds.Contains(id)) throw new UnauthorizedAccessException("Branch access denied."); }
    private static BranchDto Map(Branch x) => new(x.Id, x.LegalEntityId, x.Name.De, x.Name.Tr, x.Code, x.Address, x.ManagerName, x.Phone, x.OpenedOn, x.IsActive);
}
