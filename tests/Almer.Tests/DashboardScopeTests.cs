using Almer.Application;
using Almer.Domain;

namespace Almer.Tests;
public sealed class DashboardScopeTests
{
    [Fact]
    public async Task Unfiltered_dashboard_only_aggregates_allowed_branches()
    {
        var tenantId = Guid.NewGuid(); var allowed = Guid.NewGuid(); var denied = Guid.NewGuid(); var date = new DateOnly(2026, 11, 5);
        var data = new FakeData(
            [new Tenant { Id = tenantId, Name = "T", Slug = "t" }],
            [new Branch { Id = allowed, TenantId = tenantId, Name = new("Allowed", "İzinli"), Code = "A" }, new Branch { Id = denied, TenantId = tenantId, Name = new("Denied", "Yasak"), Code = "B" }],
            [new DailyMetric { TenantId = tenantId, BranchId = allowed, Date = date, GrossSales = 100m, SalesLines = 1, CostMatchedLines = 1 }, new DailyMetric { TenantId = tenantId, BranchId = denied, Date = date, GrossSales = 900m, SalesLines = 1, CostMatchedLines = 1 }],
            [new Product { Id = Guid.Parse("10000000-0000-0000-0000-000000000099"), TenantId = tenantId, Name = new("P", "P"), PurchaseCost = 10m, MinimumStock = 5m, IsActive = true }],
            [new StockMovement { TenantId = tenantId, BranchId = allowed, ProductId = Guid.Parse("10000000-0000-0000-0000-000000000099"), QuantityDelta = 10m }, new StockMovement { TenantId = tenantId, BranchId = denied, ProductId = Guid.Parse("10000000-0000-0000-0000-000000000099"), QuantityDelta = 100m }]);
        var service = new DashboardService(data, new TestTenantContext(tenantId, new HashSet<Guid> { allowed }));
        var result = await service.GetAsync(new(date, date, null), "de-DE", default);
        Assert.Equal(100m, result.Kpis.Single(x => x.Code == "revenue").Value);
        Assert.Equal(100m, result.Kpis.Single(x => x.Code == "stockValue").Value);
        Assert.Single(result.Branches); Assert.Equal(allowed, result.Branches[0].BranchId);
    }

    private sealed class TestTenantContext(Guid id, IReadOnlySet<Guid> branches) : ITenantContext { public Guid TenantId => id; public Guid UserId => Guid.NewGuid(); public IReadOnlySet<Guid> AllowedBranchIds => branches; }
    private sealed class FakeData(IReadOnlyList<Tenant> tenants, IReadOnlyList<Branch> branches, IReadOnlyList<DailyMetric> metrics, IReadOnlyList<Product> products, IReadOnlyList<StockMovement> movements) : IAlmerData
    {
        public IQueryable<Tenant> Tenants => tenants.AsQueryable(); public IQueryable<LegalEntity> LegalEntities => Array.Empty<LegalEntity>().AsQueryable(); public IQueryable<Branch> Branches => branches.AsQueryable(); public IQueryable<DailyMetric> DailyMetrics => metrics.AsQueryable(); public IQueryable<Role> Roles => Array.Empty<Role>().AsQueryable(); public IQueryable<User> Users => Array.Empty<User>().AsQueryable(); public IQueryable<Product> Products => products.AsQueryable(); public IQueryable<StockMovement> StockMovements => movements.AsQueryable(); public IQueryable<TenantSetting> TenantSettings => Array.Empty<TenantSetting>().AsQueryable();
        public void Add<T>(T entity) where T : class => throw new NotSupportedException(); public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
