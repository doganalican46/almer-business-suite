using Almer.Application;
using Almer.Domain;
using Almer.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Almer.Tests;
public sealed class TenantIsolationTests
{
    [Fact]
    public async Task Query_filter_and_write_guard_isolate_tenants()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var context = new TestTenantContext(a);
        var options = new DbContextOptionsBuilder<AlmerDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AlmerDbContext(options, context) { AllowDevelopmentSeed = true };
        db.BranchSet.Add(new Branch { Id = Guid.NewGuid(), TenantId = a, LegalEntityId = Guid.NewGuid(), Name = new("A", "A"), Code = "A" });
        db.BranchSet.Add(new Branch { Id = Guid.NewGuid(), TenantId = b, LegalEntityId = Guid.NewGuid(), Name = new("B", "B"), Code = "B" });
        db.ImportBatchSet.Add(new ImportBatch { Id = Guid.NewGuid(), TenantId = a, Type = ImportType.Product, FileName = "a.csv", FileHash = "A", CreatedByUserId = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow });
        db.ImportBatchSet.Add(new ImportBatch { Id = Guid.NewGuid(), TenantId = b, Type = ImportType.Product, FileName = "b.csv", FileHash = "B", CreatedByUserId = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow });
        db.TenantSettingSet.Add(new TenantSetting { Id = Guid.NewGuid(), TenantId = a, PrimaryColor = "#111111" });
        db.TenantSettingSet.Add(new TenantSetting { Id = Guid.NewGuid(), TenantId = b, PrimaryColor = "#222222" });
        await db.SaveChangesAsync(); db.AllowDevelopmentSeed = false;
        Assert.Single(await db.BranchSet.ToListAsync());
        Assert.Equal("a.csv", Assert.Single(await db.ImportBatchSet.ToListAsync()).FileName);
        Assert.Equal("#111111", Assert.Single(await db.TenantSettingSet.ToListAsync()).PrimaryColor);
        db.BranchSet.Add(new Branch { Id = Guid.NewGuid(), TenantId = b, LegalEntityId = Guid.NewGuid(), Name = new("Bad", "Bad"), Code = "BAD" });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => db.SaveChangesAsync());
    }
    private sealed class TestTenantContext(Guid tenantId) : ITenantContext { public Guid TenantId => tenantId; public Guid UserId => Guid.NewGuid(); public IReadOnlySet<Guid> AllowedBranchIds => new HashSet<Guid>(); }
}
