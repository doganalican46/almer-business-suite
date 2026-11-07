using Almer.Domain;
using Microsoft.EntityFrameworkCore;

namespace Almer.Infrastructure;

public static class DevelopmentSeeder
{
    public static readonly Guid TenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid RoleId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid UserId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    public static readonly Guid LegalEntityId = Guid.Parse("40000000-0000-0000-0000-000000000001");

    public static async Task SeedAsync(AlmerDbContext db)
    {
        if (await db.TenantSet.AnyAsync())
        {
            db.AllowDevelopmentSeed = true;
            var existing = await db.RolePermissionSet.IgnoreQueryFilters().Where(x => x.TenantId == TenantId && x.RoleId == RoleId).Select(x => x.PermissionCode).ToListAsync();
            foreach (var permission in PermissionCodes.All.Except(existing)) db.RolePermissionSet.Add(new RolePermission { TenantId = TenantId, RoleId = RoleId, PermissionCode = permission });
            var existingBranches = await db.BranchSet.IgnoreQueryFilters().Where(x => x.TenantId == TenantId && x.IsActive).ToArrayAsync();
            await SeedCatalogAsync(db, existingBranches);
            await SeedCommerceAsync(db, existingBranches);
            await db.SaveChangesAsync(); db.AllowDevelopmentSeed = false; return;
        }
        db.AllowDevelopmentSeed = true;
        db.TenantSet.Add(new Tenant { Id = TenantId, Name = "Almer Feinkost Demo", Slug = "almer-demo", IsDemo = true });
        db.LegalEntitySet.Add(new LegalEntity { Id = LegalEntityId, TenantId = TenantId, Name = new("Almer Feinkost GmbH", "Almer Feinkost GmbH"), TradeName = "Almer Supermarkt", TaxNumber = "DEMO-DE-000", CountryCode = "DE" });
        var cities = new[] { ("Grevenbroich", "GVB"), ("Bergheim", "BGM"), ("Düren", "DUR"), ("Alsdorf", "ALD"), ("Mönchengladbach (geplant)", "MGB") };
        var branches = cities.Select((x, i) => new Branch { Id = Guid.NewGuid(), TenantId = TenantId, LegalEntityId = LegalEntityId, Name = new(x.Item1, x.Item1.Replace("(geplant)", "(planlanan)")), Code = x.Item2, Address = $"Demo Adresse {i + 1}, NRW", IsActive = i < 4 }).ToArray();
        db.BranchSet.AddRange(branches);
        var role = new Role { Id = RoleId, TenantId = TenantId, Name = new("Firmeninhaber", "Firma Sahibi"), IsSystem = true };
        role.Permissions = PermissionCodes.All.Select(x => new RolePermission { TenantId = TenantId, RoleId = RoleId, PermissionCode = x }).ToList();
        db.RoleSet.Add(role);
        db.UserSet.Add(new User { Id = UserId, Email = "admin@almer.demo", DisplayName = "Demo Administrator", PasswordHash = PasswordSecurity.Hash("AlmerDemo!2026"), PreferredLocale = "de-DE", Tenants = [new UserTenant { UserId = UserId, TenantId = TenantId, RoleId = RoleId }] });
        await SeedCatalogAsync(db, branches.Where(x => x.IsActive).ToArray());
        await SeedCommerceAsync(db, branches.Where(x => x.IsActive).ToArray());
        var random = new Random(2026);
        foreach (var branch in branches.Where(x => x.IsActive))
        for (var day = 0; day < 60; day++)
        {
            var gross = decimal.Round(7000m + (decimal)random.NextDouble() * 6500m, 2);
            var vat = decimal.Round(gross * 0.105m, 2);
            var matched = random.Next(94, 101);
            db.DailyMetricSet.Add(new DailyMetric { Id = Guid.NewGuid(), TenantId = TenantId, BranchId = branch.Id, Date = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-day)), GrossSales = gross, Vat = vat, Returns = decimal.Round(gross * 0.012m, 2), Discounts = decimal.Round(gross * 0.018m, 2), CostOfGoodsSold = decimal.Round(gross * 0.61m, 2), OperatingExpenses = decimal.Round(gross * 0.16m, 2), TransactionCount = random.Next(210, 430), SalesLines = 100, CostMatchedLines = matched });
        }
        await db.SaveChangesAsync();
        db.AllowDevelopmentSeed = false;
    }

    private static async Task SeedCatalogAsync(AlmerDbContext db, IReadOnlyCollection<Branch> branches)
    {
        if (await db.ProductSet.IgnoreQueryFilters().AnyAsync(x => x.TenantId == TenantId)) return;
        var produce = new ProductCategory { Id = Guid.Parse("51000000-0000-0000-0000-000000000001"), TenantId = TenantId, Name = new("Obst & Gemüse", "Meyve ve Sebze"), SortOrder = 1 };
        var bakery = new ProductCategory { Id = Guid.Parse("51000000-0000-0000-0000-000000000002"), TenantId = TenantId, Name = new("Backwaren", "Fırın Ürünleri"), SortOrder = 2 };
        var deli = new ProductCategory { Id = Guid.Parse("51000000-0000-0000-0000-000000000003"), TenantId = TenantId, Name = new("Feinkost", "Şarküteri"), SortOrder = 3 };
        var grocery = new ProductCategory { Id = Guid.Parse("51000000-0000-0000-0000-000000000004"), TenantId = TenantId, Name = new("Lebensmittel", "Gıda"), SortOrder = 4 };
        db.ProductCategorySet.AddRange(produce, bakery, deli, grocery);
        var piece = new UnitOfMeasure { Id = Guid.Parse("52000000-0000-0000-0000-000000000001"), TenantId = TenantId, Name = new("Stück", "Adet"), Symbol = "Stk" };
        var kg = new UnitOfMeasure { Id = Guid.Parse("52000000-0000-0000-0000-000000000002"), TenantId = TenantId, Name = new("Kilogramm", "Kilogram"), Symbol = "kg" };
        var gram = new UnitOfMeasure { Id = Guid.Parse("52000000-0000-0000-0000-000000000003"), TenantId = TenantId, Name = new("Gramm", "Gram"), Symbol = "g", BaseUnitId = kg.Id, ConversionFactor = .001m };
        var litre = new UnitOfMeasure { Id = Guid.Parse("52000000-0000-0000-0000-000000000004"), TenantId = TenantId, Name = new("Liter", "Litre"), Symbol = "l" };
        var pack = new UnitOfMeasure { Id = Guid.Parse("52000000-0000-0000-0000-000000000005"), TenantId = TenantId, Name = new("Packung", "Paket"), Symbol = "Pkg" };
        db.UnitOfMeasureSet.AddRange(piece, kg, gram, litre, pack);
        var productData = new[]
        {
            ("ALM-001", "Tomaten", "Domates", produce.Id, kg.Id, 1.25m, 2.49m, 20m),
            ("ALM-002", "Paprika", "Biber", produce.Id, kg.Id, 1.75m, 3.49m, 18m),
            ("ALM-003", "Äpfel", "Elma", produce.Id, kg.Id, 1.10m, 2.29m, 25m),
            ("ALM-004", "Baklava", "Baklava", bakery.Id, kg.Id, 7.50m, 13.90m, 8m),
            ("ALM-005", "Sucuk", "Sucuk", deli.Id, piece.Id, 3.80m, 6.99m, 12m),
            ("ALM-006", "Käse", "Peynir", deli.Id, kg.Id, 5.40m, 9.90m, 10m),
            ("ALM-007", "Olivenöl", "Zeytinyağı", grocery.Id, litre.Id, 6.20m, 10.99m, 8m),
            ("ALM-008", "Bulgur", "Bulgur", grocery.Id, pack.Id, 1.10m, 2.49m, 15m),
            ("ALM-009", "Brot", "Ekmek", bakery.Id, piece.Id, .65m, 1.29m, 20m)
        };
        var now = DateTimeOffset.UtcNow; var random = new Random(3030);
        foreach (var item in productData)
        {
            var product = new Product { Id = Guid.NewGuid(), TenantId = TenantId, Sku = item.Item1, Barcode = $"400000{random.Next(100000, 999999)}", Name = new(item.Item2, item.Item3), Description = new("Synthetischer Demoartikel", "Sentetik demo ürünü"), CategoryId = item.Item4, UnitId = item.Item5, PurchaseCost = item.Item6, SalesPrice = item.Item7, MinimumStock = item.Item8, TaxClassCode = "DE-7" };
            db.ProductSet.Add(product); db.ProductPriceSet.Add(new ProductPrice { Id = Guid.NewGuid(), TenantId = TenantId, ProductId = product.Id, Price = product.SalesPrice, ValidFrom = now.AddDays(-60) });
            foreach (var branch in branches) db.StockMovementSet.Add(new StockMovement { Id = Guid.NewGuid(), TenantId = TenantId, BranchId = branch.Id, ProductId = product.Id, Type = StockMovementType.Opening, QuantityDelta = random.Next(25, 120), UnitCost = product.PurchaseCost, Reference = "DEMO-OPENING", Note = "Synthetischer Demo-Bestand", CreatedByUserId = UserId, OccurredAt = now.AddDays(-30) });
        }
    }

    private static async Task SeedCommerceAsync(AlmerDbContext db, IReadOnlyCollection<Branch> branches)
    {
        if (!await db.TaxRuleSet.IgnoreQueryFilters().AnyAsync(x => x.TenantId == TenantId))
        {
            db.TaxRuleSet.AddRange(
                new TaxRule { Id = Guid.NewGuid(), TenantId = TenantId, Code = "DE-19", CountryCode = "DE", Name = new("Regelsteuersatz", "Almanya standart KDV"), Rate = 19m, ValidFrom = new DateOnly(2020, 1, 1) },
                new TaxRule { Id = Guid.NewGuid(), TenantId = TenantId, Code = "DE-7", CountryCode = "DE", Name = new("Ermäßigter Steuersatz", "Almanya indirimli KDV"), Rate = 7m, ValidFrom = new DateOnly(2020, 1, 1) },
                new TaxRule { Id = Guid.NewGuid(), TenantId = TenantId, Code = "DE-0", CountryCode = "DE", Name = new("Steuerfrei / Sonderfall", "Vergisiz / özel durum"), Rate = 0m, ValidFrom = new DateOnly(2020, 1, 1) });
        }
        if (!await db.FinancialCategorySet.IgnoreQueryFilters().AnyAsync(x => x.TenantId == TenantId))
        {
            var expenses = new[] { ("Personal", "Personel"), ("Miete", "Kira"), ("Strom", "Elektrik"), ("Wasser", "Su"), ("Logistik", "Lojistik"), ("Software", "Yazılım"), ("Sonstige", "Diğer") };
            db.FinancialCategorySet.AddRange(expenses.Select(x => new FinancialCategory { Id = Guid.NewGuid(), TenantId = TenantId, Type = FinancialEntryType.Expense, Name = new(x.Item1, x.Item2) }));
            db.FinancialCategorySet.Add(new FinancialCategory { Id = Guid.NewGuid(), TenantId = TenantId, Type = FinancialEntryType.Income, Name = new("Sonstige Einnahmen", "Diğer Gelirler") });
        }
        if (!await db.SupplierSet.IgnoreQueryFilters().AnyAsync(x => x.TenantId == TenantId)) db.SupplierSet.Add(new Supplier { Id = Guid.NewGuid(), TenantId = TenantId, Code = "DEMO-LF-01", Name = "Demo Lebensmittel Lieferant GmbH", TaxNumber = "DEMO-SUPPLIER", Email = "demo@example.invalid", IsActive = true });
        if (!await db.CampaignSet.IgnoreQueryFilters().AnyAsync(x => x.TenantId == TenantId)) db.CampaignSet.Add(new Campaign { Id = Guid.NewGuid(), TenantId = TenantId, Name = new("Demo-Wochenaktion", "Demo haftalık kampanya"), Type = CampaignType.Percentage, Value = 5m, StartsAt = DateTimeOffset.UtcNow.AddDays(-7), EndsAt = DateTimeOffset.UtcNow.AddDays(30), Priority = 10, IsActive = true });
    }
}
