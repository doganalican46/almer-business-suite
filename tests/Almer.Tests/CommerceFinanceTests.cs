using Almer.Application;
using Almer.Domain;
using Almer.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Almer.Tests;
public sealed class CommerceFinanceTests
{
    [Fact]
    public void Gross_tax_calculation_uses_decimal_and_rounds_to_cents()
    {
        var result = TaxCalculator.FromGross(10.99m, 7m);
        Assert.Equal(10.27m, result.Net); Assert.Equal(.72m, result.Tax); Assert.Equal(10.99m, result.Gross);
    }

    [Fact]
    public void Profitability_uses_sales_snapshot_returns_and_only_approved_expenses()
    {
        var productId = Guid.NewGuid();
        var lines = new[] { new SaleLine { ProductId = productId, Quantity = 10m, UnitCost = 4m, NetAmount = 80m }, new SaleLine { ProductId = productId, Quantity = -2m, UnitCost = 4m, NetAmount = -16m } };
        var entries = new[] { new FinancialEntry { Type = FinancialEntryType.Expense, NetAmount = 10m, IsApproved = true }, new FinancialEntry { Type = FinancialEntryType.Expense, NetAmount = 999m, IsApproved = false }, new FinancialEntry { Type = FinancialEntryType.Income, NetAmount = 3m, IsApproved = true } };
        var result = ProfitabilityCalculator.Calculate(lines, entries);
        Assert.Equal(64m, result.NetSales); Assert.Equal(32m, result.CostOfGoodsSold); Assert.Equal(32m, result.GrossProfit); Assert.Equal(25m, result.OperatingResult);
    }

    [Fact]
    public void Inventory_purchase_is_not_an_operating_expense()
    {
        var result = ProfitabilityCalculator.Calculate([new SaleLine { Quantity = 1m, UnitCost = 5m, NetAmount = 8m }], []);
        Assert.Equal(5m, result.CostOfGoodsSold); Assert.Equal(0m, result.OperatingExpenses); Assert.Equal(3m, result.OperatingResult);
    }

    [Fact]
    public async Task Posted_sale_cannot_be_rewritten()
    {
        var tenantId = Guid.NewGuid(); var context = new TestTenantContext(tenantId); var options = new DbContextOptionsBuilder<AlmerDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AlmerDbContext(options, context) { AllowDevelopmentSeed = true }; var sale = new Sale { Id = Guid.NewGuid(), TenantId = tenantId, BranchId = Guid.NewGuid(), TransactionNumber = "S-1", CreatedByUserId = context.UserId };
        db.SaleSet.Add(sale); await db.SaveChangesAsync(); db.AllowDevelopmentSeed = false; sale.GrossAmount = 999m;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
    private sealed class TestTenantContext(Guid tenantId) : ITenantContext { public Guid TenantId => tenantId; public Guid UserId { get; } = Guid.NewGuid(); public IReadOnlySet<Guid> AllowedBranchIds => new HashSet<Guid>(); }
}
