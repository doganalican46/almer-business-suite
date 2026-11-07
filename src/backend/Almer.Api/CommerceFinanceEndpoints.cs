using System.Data;
using Almer.Application;
using Almer.Domain;
using Almer.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Almer.Api;

public static class CommerceFinanceEndpoints
{
    public static RouteGroupBuilder MapCommerceFinanceEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/tax-rules", TaxRules).RequireAnyPermission(PermissionCodes.FinanceRead, PermissionCodes.ProductsRead);
        group.MapPost("/tax-rules", CreateTaxRule).RequirePermission(PermissionCodes.FinanceManage);
        group.MapGet("/suppliers", Suppliers).RequirePermission(PermissionCodes.SuppliersManage);
        group.MapPost("/suppliers", CreateSupplier).RequirePermission(PermissionCodes.SuppliersManage);
        group.MapPut("/suppliers/{id:guid}", UpdateSupplier).RequirePermission(PermissionCodes.SuppliersManage);
        group.MapGet("/purchases", Purchases).RequirePermission(PermissionCodes.PurchasesManage);
        group.MapPost("/purchases", CreatePurchase).RequirePermission(PermissionCodes.PurchasesManage);
        group.MapGet("/sales", Sales).RequirePermission(PermissionCodes.SalesRead);
        group.MapPost("/sales", CreateSale).RequirePermission(PermissionCodes.SalesCreate);
        group.MapGet("/financial-categories", FinancialCategories).RequirePermission(PermissionCodes.FinanceRead);
        group.MapPost("/financial-categories", CreateFinancialCategory).RequirePermission(PermissionCodes.FinanceManage);
        group.MapGet("/financial-entries", FinancialEntries).RequirePermission(PermissionCodes.FinanceRead);
        group.MapPost("/financial-entries", CreateFinancialEntry).RequirePermission(PermissionCodes.FinanceManage);
        group.MapPut("/financial-entries/{id:guid}/approval", ApproveFinancialEntry).RequirePermission(PermissionCodes.FinanceManage);
        group.MapGet("/campaigns", Campaigns).RequirePermission(PermissionCodes.ProductsRead);
        group.MapPost("/campaigns", CreateCampaign).RequirePermission(PermissionCodes.CampaignsManage);
        group.MapPut("/campaigns/{id:guid}", UpdateCampaign).RequirePermission(PermissionCodes.CampaignsManage);
        group.MapGet("/profitability", Profitability).RequirePermission(PermissionCodes.FinanceRead);
        return group;
    }

    private static async Task<IResult> TaxRules(AlmerDbContext db, CancellationToken ct) => Results.Ok(await db.TaxRuleSet.OrderBy(x => x.Code).ThenByDescending(x => x.ValidFrom).ToListAsync(ct));
    private static async Task<IResult> CreateTaxRule(TaxRuleInput input, ITenantContext context, AlmerDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Code) || string.IsNullOrWhiteSpace(input.NameDe) || string.IsNullOrWhiteSpace(input.NameTr) || input.Rate is < 0 or > 100 || input.ValidTo < input.ValidFrom) throw new ArgumentException("Invalid tax rule.");
        var code = input.Code.Trim().ToUpperInvariant(); if (await db.TaxRuleSet.AnyAsync(x => x.Code == code && x.ValidFrom == input.ValidFrom, ct)) throw new ArgumentException("Tax rule already exists for this date.");
        var rule = new TaxRule { Id = Guid.NewGuid(), TenantId = context.TenantId, Code = code, CountryCode = input.CountryCode.Trim().ToUpperInvariant(), Name = new(input.NameDe, input.NameTr), Rate = input.Rate, ValidFrom = input.ValidFrom, ValidTo = input.ValidTo };
        db.TaxRuleSet.Add(rule); await db.SaveChangesAsync(ct); return Results.Created($"/api/tax-rules/{rule.Id}", rule);
    }

    private static async Task<IResult> Suppliers(AlmerDbContext db, CancellationToken ct) => Results.Ok(await db.SupplierSet.OrderBy(x => x.Name).ToListAsync(ct));
    private static async Task<IResult> CreateSupplier(SupplierInput input, ITenantContext context, AlmerDbContext db, CancellationToken ct)
    {
        ValidateSupplier(input); var code = input.Code.Trim().ToUpperInvariant(); if (await db.SupplierSet.AnyAsync(x => x.Code == code, ct)) throw new ArgumentException("Supplier code already exists.");
        var supplier = new Supplier { Id = Guid.NewGuid(), TenantId = context.TenantId, Code = code, Name = input.Name.Trim(), TaxNumber = Clean(input.TaxNumber), Email = Clean(input.Email), Phone = Clean(input.Phone), Address = Clean(input.Address) }; db.SupplierSet.Add(supplier); await db.SaveChangesAsync(ct); return Results.Created($"/api/suppliers/{supplier.Id}", supplier);
    }
    private static async Task<IResult> UpdateSupplier(Guid id, SupplierUpdate input, AlmerDbContext db, CancellationToken ct)
    {
        ValidateSupplier(input); var supplier = await db.SupplierSet.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Supplier not found."); var code = input.Code.Trim().ToUpperInvariant(); if (await db.SupplierSet.AnyAsync(x => x.Id != id && x.Code == code, ct)) throw new ArgumentException("Supplier code already exists.");
        supplier.Code = code; supplier.Name = input.Name.Trim(); supplier.TaxNumber = Clean(input.TaxNumber); supplier.Email = Clean(input.Email); supplier.Phone = Clean(input.Phone); supplier.Address = Clean(input.Address); supplier.IsActive = input.IsActive; await db.SaveChangesAsync(ct); return Results.Ok(supplier);
    }

    private static async Task<IResult> Purchases(DateOnly? from, DateOnly? to, Guid? branchId, ITenantContext context, AlmerDbContext db, CancellationToken ct)
    {
        if (branchId.HasValue) EnsureBranch(context, branchId.Value); var query = db.PurchaseSet.Include(x => x.Lines).AsQueryable(); if (from.HasValue) query = query.Where(x => x.Date >= from); if (to.HasValue) query = query.Where(x => x.Date <= to); if (branchId.HasValue) query = query.Where(x => x.BranchId == branchId); if (context.AllowedBranchIds.Count > 0) query = query.Where(x => context.AllowedBranchIds.Contains(x.BranchId)); return Results.Ok(await query.OrderByDescending(x => x.Date).Take(200).ToListAsync(ct));
    }
    private static async Task<IResult> CreatePurchase(PurchaseInput input, ITenantContext context, AlmerDbContext db, CancellationToken ct)
    {
        EnsureBranch(context, input.BranchId); if (string.IsNullOrWhiteSpace(input.DocumentNumber) || input.Lines.Length == 0 || await db.PurchaseSet.AnyAsync(x => x.DocumentNumber == input.DocumentNumber.Trim(), ct)) throw new ArgumentException("Invalid or duplicate purchase.");
        if (!await db.BranchSet.AnyAsync(x => x.Id == input.BranchId && x.IsActive, ct) || !await db.SupplierSet.AnyAsync(x => x.Id == input.SupplierId && x.IsActive, ct)) throw new ArgumentException("Active branch or supplier not found.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); var productIds = input.Lines.Select(x => x.ProductId).Distinct().ToArray(); var products = await db.ProductSet.Where(x => productIds.Contains(x.Id) && x.IsActive).ToDictionaryAsync(x => x.Id, ct); if (products.Count != productIds.Length) throw new ArgumentException("Product not found.");
        var purchase = new Purchase { Id = Guid.NewGuid(), TenantId = context.TenantId, BranchId = input.BranchId, SupplierId = input.SupplierId, DocumentNumber = input.DocumentNumber.Trim(), Date = input.Date, CreatedByUserId = context.UserId, CreatedAt = DateTimeOffset.UtcNow };
        foreach (var requested in input.Lines) { if (requested.Quantity <= 0 || requested.UnitCost < 0) throw new ArgumentException("Invalid purchase line."); var product = products[requested.ProductId]; var rate = await CurrentTaxRate(product.TaxClassCode, input.Date, db, ct); var totals = TaxCalculator.FromNet(decimal.Round(requested.Quantity * requested.UnitCost, 2), rate); purchase.Lines.Add(new PurchaseLine { Id = Guid.NewGuid(), TenantId = context.TenantId, PurchaseId = purchase.Id, ProductId = product.Id, ProductNameSnapshot = product.Name.De, Quantity = requested.Quantity, UnitCost = requested.UnitCost, TaxRate = rate, NetAmount = totals.Net, TaxAmount = totals.Tax, GrossAmount = totals.Gross }); db.StockMovementSet.Add(NewStock(context, input.BranchId, product.Id, StockMovementType.Purchase, requested.Quantity, requested.UnitCost, purchase.DocumentNumber)); product.PurchaseCost = requested.UnitCost; }
        purchase.NetAmount = purchase.Lines.Sum(x => x.NetAmount); purchase.TaxAmount = purchase.Lines.Sum(x => x.TaxAmount); purchase.GrossAmount = purchase.Lines.Sum(x => x.GrossAmount); db.PurchaseSet.Add(purchase); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return Results.Created($"/api/purchases/{purchase.Id}", purchase);
    }

    private static async Task<IResult> Sales(DateTimeOffset? from, DateTimeOffset? to, Guid? branchId, ITenantContext context, AlmerDbContext db, CancellationToken ct)
    {
        if (branchId.HasValue) EnsureBranch(context, branchId.Value); var query = db.SaleSet.Include(x => x.Lines).AsQueryable(); if (from.HasValue) query = query.Where(x => x.OccurredAt >= from); if (to.HasValue) query = query.Where(x => x.OccurredAt <= to); if (branchId.HasValue) query = query.Where(x => x.BranchId == branchId); if (context.AllowedBranchIds.Count > 0) query = query.Where(x => context.AllowedBranchIds.Contains(x.BranchId)); return Results.Ok(await query.OrderByDescending(x => x.OccurredAt).Take(250).ToListAsync(ct));
    }
    private static async Task<IResult> CreateSale(SaleInput input, ITenantContext context, AlmerDbContext db, CancellationToken ct)
    {
        EnsureBranch(context, input.BranchId); if (string.IsNullOrWhiteSpace(input.TransactionNumber) || input.Lines.Length == 0 || await db.SaleSet.AnyAsync(x => x.TransactionNumber == input.TransactionNumber.Trim(), ct)) throw new ArgumentException("Invalid or duplicate sale."); if (!await db.BranchSet.AnyAsync(x => x.Id == input.BranchId && x.IsActive, ct)) throw new ArgumentException("Branch not found.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); var productIds = input.Lines.Select(x => x.ProductId).Distinct().ToArray(); var products = await db.ProductSet.Where(x => productIds.Contains(x.Id) && x.IsActive).ToDictionaryAsync(x => x.Id, ct); if (products.Count != productIds.Length) throw new ArgumentException("Product not found."); var currentStocks = new Dictionary<Guid, decimal>();
        var sale = new Sale { Id = Guid.NewGuid(), TenantId = context.TenantId, BranchId = input.BranchId, TransactionNumber = input.TransactionNumber.Trim(), OccurredAt = input.OccurredAt, PaymentMethod = input.PaymentMethod.Trim(), SourceSystem = input.SourceSystem.Trim(), IsReturn = input.IsReturn, CreatedByUserId = context.UserId };
        foreach (var requested in input.Lines) { if (requested.Quantity <= 0) throw new ArgumentException("Quantity must be positive."); var product = products[requested.ProductId]; var price = requested.UnitPrice ?? await CurrentPrice(product, input.BranchId, input.OccurredAt, db, ct); if (price < 0) throw new ArgumentException("Price cannot be negative."); var grossBeforeDiscount = decimal.Round(price * requested.Quantity, 2); var discount = requested.DiscountAmount ?? await CampaignDiscount(product, input.BranchId, input.OccurredAt, grossBeforeDiscount, db, ct); if (discount < 0 || discount > grossBeforeDiscount) throw new ArgumentException("Invalid discount."); var rate = await CurrentTaxRate(product.TaxClassCode, DateOnly.FromDateTime(input.OccurredAt.UtcDateTime), db, ct); var totals = TaxCalculator.FromGross(grossBeforeDiscount - discount, rate); var sign = input.IsReturn ? -1m : 1m; sale.Lines.Add(new SaleLine { Id = Guid.NewGuid(), TenantId = context.TenantId, SaleId = sale.Id, ProductId = product.Id, ProductNameSnapshot = product.Name.De, Quantity = requested.Quantity * sign, UnitPrice = price, UnitCost = product.PurchaseCost, DiscountAmount = discount * sign, TaxRate = rate, NetAmount = totals.Net * sign, TaxAmount = totals.Tax * sign, GrossAmount = totals.Gross * sign }); if (!currentStocks.TryGetValue(product.Id, out var current)) current = await CurrentStock(db, input.BranchId, product.Id, ct); var movementType = input.IsReturn ? StockMovementType.CustomerReturn : StockMovementType.Sale; var delta = InventoryRules.ToDelta(movementType, requested.Quantity, current); currentStocks[product.Id] = current + delta; db.StockMovementSet.Add(NewStock(context, input.BranchId, product.Id, movementType, delta, product.PurchaseCost, sale.TransactionNumber)); }
        sale.NetAmount = sale.Lines.Sum(x => x.NetAmount); sale.TaxAmount = sale.Lines.Sum(x => x.TaxAmount); sale.GrossAmount = sale.Lines.Sum(x => x.GrossAmount); sale.DiscountAmount = sale.Lines.Sum(x => x.DiscountAmount); db.SaleSet.Add(sale); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return Results.Created($"/api/sales/{sale.Id}", sale);
    }

    private static async Task<IResult> FinancialCategories(AlmerDbContext db, CancellationToken ct) => Results.Ok(await db.FinancialCategorySet.OrderBy(x => x.Type).ThenBy(x => x.Name.De).ToListAsync(ct));
    private static async Task<IResult> CreateFinancialCategory(FinancialCategoryInput input, ITenantContext context, AlmerDbContext db, CancellationToken ct) { if (string.IsNullOrWhiteSpace(input.NameDe) || string.IsNullOrWhiteSpace(input.NameTr)) throw new ArgumentException("Names are required."); var category = new FinancialCategory { Id = Guid.NewGuid(), TenantId = context.TenantId, Type = input.Type, Name = new(input.NameDe, input.NameTr) }; db.FinancialCategorySet.Add(category); await db.SaveChangesAsync(ct); return Results.Created($"/api/financial-categories/{category.Id}", category); }
    private static async Task<IResult> FinancialEntries(DateOnly? from, DateOnly? to, Guid? branchId, ITenantContext context, AlmerDbContext db, CancellationToken ct) { if (branchId.HasValue) EnsureBranch(context, branchId.Value); var query = db.FinancialEntrySet.AsQueryable(); if (from.HasValue) query = query.Where(x => x.Date >= from); if (to.HasValue) query = query.Where(x => x.Date <= to); if (branchId.HasValue) query = query.Where(x => x.BranchId == branchId); if (context.AllowedBranchIds.Count > 0) query = query.Where(x => x.BranchId == null || context.AllowedBranchIds.Contains(x.BranchId.Value)); return Results.Ok(await query.OrderByDescending(x => x.Date).Take(500).ToListAsync(ct)); }
    private static async Task<IResult> CreateFinancialEntry(FinancialEntryInput input, ITenantContext context, AlmerDbContext db, CancellationToken ct)
    {
        if (input.BranchId.HasValue) EnsureBranch(context, input.BranchId.Value); var category = await db.FinancialCategorySet.SingleOrDefaultAsync(x => x.Id == input.CategoryId && x.IsActive, ct) ?? throw new ArgumentException("Category not found."); if (category.Type != input.Type || input.NetAmount < 0 || input.TaxRate is < 0 or > 100) throw new ArgumentException("Invalid financial entry."); var totals = TaxCalculator.FromNet(input.NetAmount, input.TaxRate); var entry = new FinancialEntry { Id = Guid.NewGuid(), TenantId = context.TenantId, BranchId = input.BranchId, CategoryId = input.CategoryId, Type = input.Type, Date = input.Date, NetAmount = totals.Net, TaxRate = input.TaxRate, TaxAmount = totals.Tax, GrossAmount = totals.Gross, Description = input.Description.Trim(), PaymentMethod = input.PaymentMethod.Trim(), IsRecurring = input.IsRecurring, IsApproved = false, DocumentUrl = Clean(input.DocumentUrl), CreatedByUserId = context.UserId, CreatedAt = DateTimeOffset.UtcNow }; db.FinancialEntrySet.Add(entry); await db.SaveChangesAsync(ct); return Results.Created($"/api/financial-entries/{entry.Id}", entry);
    }
    private static async Task<IResult> ApproveFinancialEntry(Guid id, ApprovalInput input, AlmerDbContext db, CancellationToken ct) { var entry = await db.FinancialEntrySet.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Entry not found."); entry.IsApproved = input.IsApproved; await db.SaveChangesAsync(ct); return Results.Ok(entry); }

    private static async Task<IResult> Campaigns(AlmerDbContext db, CancellationToken ct) => Results.Ok(await db.CampaignSet.OrderByDescending(x => x.StartsAt).ToListAsync(ct));
    private static async Task<IResult> CreateCampaign(CampaignInput input, ITenantContext context, AlmerDbContext db, CancellationToken ct) { await ValidateCampaign(input, db, ct); var campaign = new Campaign { Id = Guid.NewGuid(), TenantId = context.TenantId }; Apply(campaign, input); db.CampaignSet.Add(campaign); await db.SaveChangesAsync(ct); return Results.Created($"/api/campaigns/{campaign.Id}", campaign); }
    private static async Task<IResult> UpdateCampaign(Guid id, CampaignUpdate input, AlmerDbContext db, CancellationToken ct) { await ValidateCampaign(input, db, ct); var campaign = await db.CampaignSet.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Campaign not found."); Apply(campaign, input); campaign.IsActive = input.IsActive; await db.SaveChangesAsync(ct); return Results.Ok(campaign); }

    private static async Task<IResult> Profitability(DateOnly from, DateOnly to, Guid? branchId, ITenantContext context, AlmerDbContext db, CancellationToken ct)
    {
        if (to < from || to.DayNumber - from.DayNumber > 370) throw new ArgumentException("Invalid period."); if (branchId.HasValue) EnsureBranch(context, branchId.Value); var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero); var end = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero); var sales = db.SaleSet.Where(x => x.OccurredAt >= start && x.OccurredAt < end); var entries = db.FinancialEntrySet.Where(x => x.Date >= from && x.Date <= to && x.IsApproved); if (branchId.HasValue) { sales = sales.Where(x => x.BranchId == branchId); entries = entries.Where(x => x.BranchId == branchId || x.BranchId == null); } if (context.AllowedBranchIds.Count > 0) { sales = sales.Where(x => context.AllowedBranchIds.Contains(x.BranchId)); entries = entries.Where(x => x.BranchId == null || context.AllowedBranchIds.Contains(x.BranchId.Value)); }
        var saleRows = await sales.Include(x => x.Lines).ToListAsync(ct); var entryRows = await entries.ToListAsync(ct); var lines = saleRows.SelectMany(x => x.Lines).ToArray(); var summary = ProfitabilityCalculator.Calculate(lines, entryRows); var byProduct = lines.GroupBy(x => new { x.ProductId, x.ProductNameSnapshot }).Select(x => new { x.Key.ProductId, name = x.Key.ProductNameSnapshot, netSales = x.Sum(y => y.NetAmount), cost = x.Sum(y => y.Quantity * y.UnitCost), grossProfit = x.Sum(y => y.NetAmount - y.Quantity * y.UnitCost) }).OrderByDescending(x => x.grossProfit).ToArray();
        return Results.Ok(new { summary.NetSales, summary.CostOfGoodsSold, summary.GrossProfit, summary.GrossMargin, summary.OperatingExpenses, summary.OtherIncome, summary.OperatingResult, dataQuality = new { summary.CostMatchRate, summary.MissingCostLines, summary.IsComplete }, byProduct });
    }

    private static async Task<decimal> CurrentTaxRate(string code, DateOnly date, AlmerDbContext db, CancellationToken ct) => (await db.TaxRuleSet.Where(x => x.Code == code && x.IsActive && x.ValidFrom <= date && (x.ValidTo == null || x.ValidTo >= date)).OrderByDescending(x => x.ValidFrom).Select(x => (decimal?)x.Rate).FirstOrDefaultAsync(ct)) ?? throw new InvalidOperationException($"No valid tax rule for {code} on {date}.");
    private static async Task<decimal> CurrentPrice(Product product, Guid branchId, DateTimeOffset time, AlmerDbContext db, CancellationToken ct) => await db.ProductPriceSet.Where(x => x.ProductId == product.Id && x.ValidFrom <= time && (x.ValidTo == null || x.ValidTo > time) && (x.BranchId == branchId || x.BranchId == null)).OrderByDescending(x => x.BranchId != null).ThenByDescending(x => x.ValidFrom).Select(x => (decimal?)x.Price).FirstOrDefaultAsync(ct) ?? product.SalesPrice;
    private static async Task<decimal> CampaignDiscount(Product product, Guid branchId, DateTimeOffset time, decimal gross, AlmerDbContext db, CancellationToken ct) { var campaign = await db.CampaignSet.Where(x => x.IsActive && x.StartsAt <= time && x.EndsAt >= time && (x.BranchId == null || x.BranchId == branchId) && (x.ProductId == product.Id || (x.ProductId == null && x.CategoryId == product.CategoryId) || (x.ProductId == null && x.CategoryId == null))).OrderByDescending(x => x.Priority).FirstOrDefaultAsync(ct); if (campaign is null) return 0; return campaign.Type == CampaignType.Percentage ? decimal.Round(gross * campaign.Value / 100m, 2) : Math.Min(campaign.Value, gross); }
    private static async Task<decimal> CurrentStock(AlmerDbContext db, Guid branchId, Guid productId, CancellationToken ct) => await db.StockMovementSet.Where(x => x.BranchId == branchId && x.ProductId == productId).SumAsync(x => (decimal?)x.QuantityDelta, ct) ?? 0m;
    private static StockMovement NewStock(ITenantContext c, Guid branch, Guid product, StockMovementType type, decimal delta, decimal? cost, string reference) => new() { Id = Guid.NewGuid(), TenantId = c.TenantId, BranchId = branch, ProductId = product, Type = type, QuantityDelta = delta, UnitCost = cost, Reference = reference, CreatedByUserId = c.UserId, OccurredAt = DateTimeOffset.UtcNow };
    private static async Task ValidateCampaign(CampaignInput input, AlmerDbContext db, CancellationToken ct) { if (string.IsNullOrWhiteSpace(input.NameDe) || string.IsNullOrWhiteSpace(input.NameTr) || input.Value < 0 || input.EndsAt <= input.StartsAt || input.Type == CampaignType.Percentage && input.Value > 100) throw new ArgumentException("Invalid campaign."); if (input.ProductId.HasValue && !await db.ProductSet.AnyAsync(x => x.Id == input.ProductId, ct)) throw new ArgumentException("Product not found."); if (input.CategoryId.HasValue && !await db.ProductCategorySet.AnyAsync(x => x.Id == input.CategoryId, ct)) throw new ArgumentException("Category not found."); if (input.BranchId.HasValue && !await db.BranchSet.AnyAsync(x => x.Id == input.BranchId, ct)) throw new ArgumentException("Branch not found."); }
    private static void Apply(Campaign value, CampaignInput input) { value.Name = new(input.NameDe, input.NameTr); value.Type = input.Type; value.Value = input.Value; value.ProductId = input.ProductId; value.CategoryId = input.CategoryId; value.BranchId = input.BranchId; value.StartsAt = input.StartsAt; value.EndsAt = input.EndsAt; value.Priority = input.Priority; }
    private static void ValidateSupplier(SupplierInput input) { if (string.IsNullOrWhiteSpace(input.Code) || string.IsNullOrWhiteSpace(input.Name)) throw new ArgumentException("Supplier code and name are required."); }
    private static void EnsureBranch(ITenantContext context, Guid branchId) { if (context.AllowedBranchIds.Count > 0 && !context.AllowedBranchIds.Contains(branchId)) throw new UnauthorizedAccessException("Branch access denied."); }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record TaxRuleInput(string Code, string CountryCode, string NameDe, string NameTr, decimal Rate, DateOnly ValidFrom, DateOnly? ValidTo);
public record SupplierInput(string Code, string Name, string? TaxNumber, string? Email, string? Phone, string? Address);
public sealed record SupplierUpdate(string Code, string Name, string? TaxNumber, string? Email, string? Phone, string? Address, bool IsActive) : SupplierInput(Code, Name, TaxNumber, Email, Phone, Address);
public sealed record PurchaseLineInput(Guid ProductId, decimal Quantity, decimal UnitCost);
public sealed record PurchaseInput(Guid BranchId, Guid SupplierId, string DocumentNumber, DateOnly Date, PurchaseLineInput[] Lines);
public sealed record SaleLineInput(Guid ProductId, decimal Quantity, decimal? UnitPrice, decimal? DiscountAmount);
public sealed record SaleInput(Guid BranchId, string TransactionNumber, DateTimeOffset OccurredAt, string PaymentMethod, string SourceSystem, bool IsReturn, SaleLineInput[] Lines);
public sealed record FinancialCategoryInput(FinancialEntryType Type, string NameDe, string NameTr);
public sealed record FinancialEntryInput(Guid? BranchId, Guid CategoryId, FinancialEntryType Type, DateOnly Date, decimal NetAmount, decimal TaxRate, string Description, string PaymentMethod, bool IsRecurring, string? DocumentUrl);
public sealed record ApprovalInput(bool IsApproved);
public record CampaignInput(string NameDe, string NameTr, CampaignType Type, decimal Value, Guid? ProductId, Guid? CategoryId, Guid? BranchId, DateTimeOffset StartsAt, DateTimeOffset EndsAt, int Priority);
public sealed record CampaignUpdate(string NameDe, string NameTr, CampaignType Type, decimal Value, Guid? ProductId, Guid? CategoryId, Guid? BranchId, DateTimeOffset StartsAt, DateTimeOffset EndsAt, int Priority, bool IsActive) : CampaignInput(NameDe, NameTr, Type, Value, ProductId, CategoryId, BranchId, StartsAt, EndsAt, Priority);
