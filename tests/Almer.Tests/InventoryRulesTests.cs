using Almer.Application;
using Almer.Domain;
using Almer.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Almer.Tests;
public sealed class InventoryRulesTests
{
    [Theory]
    [InlineData(StockMovementType.Purchase, 5, 10, 5)]
    [InlineData(StockMovementType.Sale, 5, 10, -5)]
    [InlineData(StockMovementType.Waste, 2, 10, -2)]
    [InlineData(StockMovementType.CountAdjustment, 7, 10, -3)]
    public void Movement_types_produce_expected_delta(StockMovementType type, decimal quantity, decimal current, decimal expected) => Assert.Equal(expected, InventoryRules.ToDelta(type, quantity, current));

    [Fact]
    public void Outgoing_movement_cannot_make_stock_negative() => Assert.Throws<InvalidOperationException>(() => InventoryRules.ToDelta(StockMovementType.Sale, 11m, 10m));

    [Fact]
    public void Transfer_pair_preserves_total_quantity()
    {
        var outgoing = InventoryRules.ToDelta(StockMovementType.TransferOut, 4m, 10m);
        var incoming = InventoryRules.ToDelta(StockMovementType.TransferIn, 4m, 2m);
        Assert.Equal(0m, outgoing + incoming);
    }

    [Fact]
    public async Task Persisted_stock_movement_cannot_be_changed_or_deleted()
    {
        var tenantId = Guid.NewGuid(); var context = new TestTenantContext(tenantId); var options = new DbContextOptionsBuilder<AlmerDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AlmerDbContext(options, context) { AllowDevelopmentSeed = true };
        var movement = new StockMovement { Id = Guid.NewGuid(), TenantId = tenantId, BranchId = Guid.NewGuid(), ProductId = Guid.NewGuid(), Type = StockMovementType.Opening, QuantityDelta = 10m, CreatedByUserId = Guid.NewGuid(), OccurredAt = DateTimeOffset.UtcNow };
        db.StockMovementSet.Add(movement); await db.SaveChangesAsync(); db.AllowDevelopmentSeed = false;
        movement.QuantityDelta = 11m;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
    private sealed class TestTenantContext(Guid tenantId) : ITenantContext { public Guid TenantId => tenantId; public Guid UserId => Guid.NewGuid(); public IReadOnlySet<Guid> AllowedBranchIds => new HashSet<Guid>(); }
}
