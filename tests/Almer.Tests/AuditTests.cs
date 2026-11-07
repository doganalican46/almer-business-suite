using Almer.Application;
using Almer.Domain;
using Almer.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Almer.Tests;
public sealed class AuditTests
{
    [Fact]
    public async Task Tenant_write_creates_audit_entry_with_actor()
    {
        var tenantId = Guid.NewGuid(); var userId = Guid.NewGuid(); var context = new TestTenantContext(tenantId, userId);
        var options = new DbContextOptionsBuilder<AlmerDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AlmerDbContext(options, context) { AllowDevelopmentSeed = true };
        db.TenantSet.Add(new Tenant { Id = tenantId, Name = "Test", Slug = "test" });
        db.LegalEntitySet.Add(new LegalEntity { Id = Guid.NewGuid(), TenantId = tenantId, Name = new("Firma", "Firma"), TradeName = "Firma", TaxNumber = "T-1" });
        await db.SaveChangesAsync(); db.AllowDevelopmentSeed = false;
        var entity = await db.LegalEntitySet.SingleAsync(); entity.TradeName = "Updated";
        await db.SaveChangesAsync();
        var audit = await db.AuditEntrySet.SingleAsync();
        Assert.Equal(userId, audit.UserId); Assert.Equal("LegalEntity", audit.EntityType); Assert.Equal("Modified", audit.Action);
    }
    private sealed class TestTenantContext(Guid tenantId, Guid userId) : ITenantContext { public Guid TenantId => tenantId; public Guid UserId => userId; public IReadOnlySet<Guid> AllowedBranchIds => new HashSet<Guid>(); }
}
