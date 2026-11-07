using System.Data;
using Almer.Application;
using Almer.Domain;
using Almer.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Almer.Api;

public static class CatalogInventoryEndpoints
{
    public static RouteGroupBuilder MapCatalogInventoryEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/categories", ListCategories).RequirePermission(PermissionCodes.ProductsRead);
        group.MapPost("/categories", CreateCategory).RequirePermission(PermissionCodes.ProductsManage);
        group.MapPut("/categories/{id:guid}", UpdateCategory).RequirePermission(PermissionCodes.ProductsManage);
        group.MapGet("/units", ListUnits).RequirePermission(PermissionCodes.ProductsRead);
        group.MapPost("/units", CreateUnit).RequirePermission(PermissionCodes.ProductsManage);
        group.MapPut("/units/{id:guid}", UpdateUnit).RequirePermission(PermissionCodes.ProductsManage);
        group.MapGet("/products", ListProducts).RequirePermission(PermissionCodes.ProductsRead);
        group.MapPost("/products", CreateProduct).RequirePermission(PermissionCodes.ProductsManage);
        group.MapPut("/products/{id:guid}", UpdateProduct).RequirePermission(PermissionCodes.ProductsManage);
        group.MapGet("/products/{id:guid}/prices", ProductPrices).RequirePermission(PermissionCodes.ProductsRead);
        group.MapPost("/products/{id:guid}/prices", SetBranchPrice).RequirePermission(PermissionCodes.ProductsManage);
        group.MapGet("/inventory/balances", InventoryBalances).RequirePermission(PermissionCodes.InventoryRead);
        group.MapGet("/inventory/movements", InventoryMovements).RequirePermission(PermissionCodes.InventoryRead);
        group.MapPost("/inventory/movements", CreateMovement).RequirePermission(PermissionCodes.InventoryAdjust);
        group.MapPost("/inventory/transfers", Transfer).RequirePermission(PermissionCodes.InventoryTransfer);
        return group;
    }

    private static async Task<IResult> ListCategories(AlmerDbContext db, CancellationToken ct) => Results.Ok((await db.ProductCategorySet.OrderBy(x => x.SortOrder).ToListAsync(ct)).Select(Map));
    private static async Task<IResult> CreateCategory(CategoryInput input, ITenantContext context, AlmerDbContext db, CancellationToken ct)
    {
        ValidateNames(input.NameDe, input.NameTr); if (input.ParentId.HasValue && !await db.ProductCategorySet.AnyAsync(x => x.Id == input.ParentId, ct)) throw new ArgumentException("Parent category was not found.");
        var value = new ProductCategory { Id = Guid.NewGuid(), TenantId = context.TenantId, ParentId = input.ParentId, Name = new(input.NameDe, input.NameTr), SortOrder = input.SortOrder };
        db.ProductCategorySet.Add(value); await db.SaveChangesAsync(ct); return Results.Created($"/api/categories/{value.Id}", Map(value));
    }
    private static async Task<IResult> UpdateCategory(Guid id, CategoryUpdate input, AlmerDbContext db, CancellationToken ct)
    {
        ValidateNames(input.NameDe, input.NameTr); if (input.ParentId == id) throw new ArgumentException("A category cannot be its own parent.");
        if (input.ParentId.HasValue && !await db.ProductCategorySet.AnyAsync(x => x.Id == input.ParentId, ct)) throw new ArgumentException("Parent category was not found.");
        await EnsureNoCategoryCycle(id, input.ParentId, db, ct);
        var value = await db.ProductCategorySet.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Category was not found.");
        value.ParentId = input.ParentId; value.Name = new(input.NameDe, input.NameTr); value.SortOrder = input.SortOrder; value.IsActive = input.IsActive; await db.SaveChangesAsync(ct); return Results.Ok(Map(value));
    }

    private static async Task<IResult> ListUnits(AlmerDbContext db, CancellationToken ct) => Results.Ok((await db.UnitOfMeasureSet.OrderBy(x => x.Symbol).ToListAsync(ct)).Select(Map));
    private static async Task<IResult> CreateUnit(UnitInput input, ITenantContext context, AlmerDbContext db, CancellationToken ct)
    {
        await ValidateUnit(input, null, db, ct); var value = new UnitOfMeasure { Id = Guid.NewGuid(), TenantId = context.TenantId, Name = new(input.NameDe, input.NameTr), Symbol = input.Symbol.Trim(), BaseUnitId = input.BaseUnitId, ConversionFactor = input.ConversionFactor };
        db.UnitOfMeasureSet.Add(value); await db.SaveChangesAsync(ct); return Results.Created($"/api/units/{value.Id}", Map(value));
    }
    private static async Task<IResult> UpdateUnit(Guid id, UnitUpdate input, AlmerDbContext db, CancellationToken ct)
    {
        if (input.BaseUnitId == id) throw new ArgumentException("A unit cannot convert to itself."); await ValidateUnit(input, id, db, ct);
        var value = await db.UnitOfMeasureSet.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Unit was not found.");
        value.Name = new(input.NameDe, input.NameTr); value.Symbol = input.Symbol.Trim(); value.BaseUnitId = input.BaseUnitId; value.ConversionFactor = input.ConversionFactor; value.IsActive = input.IsActive; await db.SaveChangesAsync(ct); return Results.Ok(Map(value));
    }

    private static async Task<IResult> ListProducts(int? page, int? pageSize, string? search, Guid? categoryId, AlmerDbContext db, CancellationToken ct)
    {
        var take = Math.Clamp(pageSize ?? 25, 1, 100); var currentPage = Math.Max(page ?? 1, 1); var query = db.ProductSet.AsQueryable();
        if (categoryId.HasValue) query = query.Where(x => x.CategoryId == categoryId);
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim().ToLower(); query = query.Where(x => x.Sku.ToLower().Contains(term) || (x.Barcode != null && x.Barcode.Contains(term)) || x.Name.De.ToLower().Contains(term) || x.Name.Tr.ToLower().Contains(term)); }
        var total = await query.CountAsync(ct); var items = await query.OrderBy(x => x.Sku).Skip((currentPage - 1) * take).Take(take).ToListAsync(ct);
        return Results.Ok(new { items = items.Select(Map), total, page = currentPage, pageSize = take });
    }
    private static async Task<IResult> CreateProduct(ProductInput input, ITenantContext context, AlmerDbContext db, CancellationToken ct)
    {
        await ValidateProduct(input, null, db, ct); var now = DateTimeOffset.UtcNow;
        var value = ToEntity(input, context.TenantId, Guid.NewGuid()); db.ProductSet.Add(value); db.ProductPriceSet.Add(new ProductPrice { Id = Guid.NewGuid(), TenantId = context.TenantId, ProductId = value.Id, Price = value.SalesPrice, ValidFrom = now });
        await db.SaveChangesAsync(ct); return Results.Created($"/api/products/{value.Id}", Map(value));
    }
    private static async Task<IResult> UpdateProduct(Guid id, ProductUpdate input, ITenantContext context, AlmerDbContext db, CancellationToken ct)
    {
        await ValidateProduct(input, id, db, ct); var value = await db.ProductSet.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Product was not found."); var previousPrice = value.SalesPrice;
        Apply(value, input); if (previousPrice != value.SalesPrice) { var now = DateTimeOffset.UtcNow; var active = await db.ProductPriceSet.Where(x => x.ProductId == id && x.BranchId == null && x.ValidTo == null).ToListAsync(ct); active.ForEach(x => x.ValidTo = now); db.ProductPriceSet.Add(new ProductPrice { Id = Guid.NewGuid(), TenantId = context.TenantId, ProductId = id, Price = value.SalesPrice, ValidFrom = now }); }
        await db.SaveChangesAsync(ct); return Results.Ok(Map(value));
    }
    private static async Task<IResult> ProductPrices(Guid id, AlmerDbContext db, CancellationToken ct) => Results.Ok(await db.ProductPriceSet.Where(x => x.ProductId == id).OrderByDescending(x => x.ValidFrom).ToListAsync(ct));
    private static async Task<IResult> SetBranchPrice(Guid id, BranchPriceInput input, ITenantContext context, AlmerDbContext db, CancellationToken ct)
    {
        EnsureBranch(context, input.BranchId); if (input.Price < 0 || !await db.ProductSet.AnyAsync(x => x.Id == id, ct) || !await db.BranchSet.AnyAsync(x => x.Id == input.BranchId, ct)) throw new ArgumentException("Invalid product, branch or price.");
        var now = DateTimeOffset.UtcNow; var active = await db.ProductPriceSet.Where(x => x.ProductId == id && x.BranchId == input.BranchId && x.ValidTo == null).ToListAsync(ct); active.ForEach(x => x.ValidTo = now);
        var price = new ProductPrice { Id = Guid.NewGuid(), TenantId = context.TenantId, ProductId = id, BranchId = input.BranchId, Price = input.Price, ValidFrom = now }; db.ProductPriceSet.Add(price); await db.SaveChangesAsync(ct); return Results.Created($"/api/products/{id}/prices", price);
    }

    private static async Task<IResult> InventoryBalances(Guid? branchId, string? search, ITenantContext context, AlmerDbContext db, CancellationToken ct)
    {
        if (branchId.HasValue) EnsureBranch(context, branchId.Value); var branches = db.BranchSet.AsQueryable(); if (branchId.HasValue) branches = branches.Where(x => x.Id == branchId); if (context.AllowedBranchIds.Count > 0) branches = branches.Where(x => context.AllowedBranchIds.Contains(x.Id));
        var branchRows = await branches.Where(x => x.IsActive).ToListAsync(ct); var products = db.ProductSet.Where(x => x.IsActive); if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim().ToLower(); products = products.Where(x => x.Sku.ToLower().Contains(term) || x.Name.De.ToLower().Contains(term) || x.Name.Tr.ToLower().Contains(term)); }
        var productRows = await products.ToListAsync(ct); var branchIds = branchRows.Select(x => x.Id).ToArray(); var productIds = productRows.Select(x => x.Id).ToArray();
        var totals = await db.StockMovementSet.Where(x => branchIds.Contains(x.BranchId) && productIds.Contains(x.ProductId)).GroupBy(x => new { x.BranchId, x.ProductId }).Select(x => new { x.Key.BranchId, x.Key.ProductId, Quantity = x.Sum(y => y.QuantityDelta) }).ToListAsync(ct);
        var result = from branch in branchRows from product in productRows let quantity = totals.FirstOrDefault(x => x.BranchId == branch.Id && x.ProductId == product.Id)?.Quantity ?? 0 select new StockBalanceDto(branch.Id, branch.Name.De, branch.Name.Tr, product.Id, product.Sku, product.Name.De, product.Name.Tr, quantity, product.MinimumStock, quantity <= product.MinimumStock);
        return Results.Ok(result);
    }
    private static async Task<IResult> InventoryMovements(Guid? branchId, Guid? productId, ITenantContext context, AlmerDbContext db, CancellationToken ct)
    {
        if (branchId.HasValue) EnsureBranch(context, branchId.Value); var query = db.StockMovementSet.AsQueryable(); if (context.AllowedBranchIds.Count > 0) query = query.Where(x => context.AllowedBranchIds.Contains(x.BranchId)); if (branchId.HasValue) query = query.Where(x => x.BranchId == branchId); if (productId.HasValue) query = query.Where(x => x.ProductId == productId);
        return Results.Ok(await query.OrderByDescending(x => x.OccurredAt).Take(250).ToListAsync(ct));
    }
    private static async Task<IResult> CreateMovement(StockMovementInput input, ITenantContext context, AlmerDbContext db, CancellationToken ct)
    {
        EnsureBranch(context, input.BranchId); if (input.Type is StockMovementType.TransferIn or StockMovementType.TransferOut) throw new ArgumentException("Use the transfer endpoint.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); var current = await CurrentStock(db, input.BranchId, input.ProductId, ct); var delta = InventoryRules.ToDelta(input.Type, input.Quantity, current);
        await ValidateStockReferences(input.BranchId, input.ProductId, db, ct); var movement = NewMovement(context, input.BranchId, input.ProductId, input.Type, delta, input.UnitCost, input.Reference, input.Note, input.LotNumber, input.ExpiresOn, null);
        db.StockMovementSet.Add(movement); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return Results.Created($"/api/inventory/movements/{movement.Id}", movement);
    }
    private static async Task<IResult> Transfer(StockTransferInput input, ITenantContext context, AlmerDbContext db, CancellationToken ct)
    {
        if (input.FromBranchId == input.ToBranchId || input.Quantity <= 0) throw new ArgumentException("Invalid transfer."); EnsureBranch(context, input.FromBranchId); EnsureBranch(context, input.ToBranchId);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); await ValidateStockReferences(input.FromBranchId, input.ProductId, db, ct); await ValidateStockReferences(input.ToBranchId, input.ProductId, db, ct);
        var current = await CurrentStock(db, input.FromBranchId, input.ProductId, ct); var outDelta = InventoryRules.ToDelta(StockMovementType.TransferOut, input.Quantity, current); var group = Guid.NewGuid();
        db.StockMovementSet.AddRange(NewMovement(context, input.FromBranchId, input.ProductId, StockMovementType.TransferOut, outDelta, null, input.Reference, input.Note, null, null, group), NewMovement(context, input.ToBranchId, input.ProductId, StockMovementType.TransferIn, input.Quantity, null, input.Reference, input.Note, null, null, group));
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return Results.Ok(new { transferGroupId = group });
    }

    private static async Task ValidateProduct(ProductInput input, Guid? id, AlmerDbContext db, CancellationToken ct)
    {
        ValidateNames(input.NameDe, input.NameTr); if (string.IsNullOrWhiteSpace(input.Sku) || input.PurchaseCost < 0 || input.SalesPrice < 0 || input.MinimumStock < 0) throw new ArgumentException("Invalid product data.");
        if (!await db.ProductCategorySet.AnyAsync(x => x.Id == input.CategoryId && x.IsActive, ct) || !await db.UnitOfMeasureSet.AnyAsync(x => x.Id == input.UnitId && x.IsActive, ct)) throw new ArgumentException("Category or unit was not found.");
        var sku = input.Sku.Trim().ToUpperInvariant(); if (await db.ProductSet.AnyAsync(x => x.Id != id && x.Sku == sku, ct)) throw new ArgumentException("SKU already exists."); var barcode = Normalize(input.Barcode); if (barcode != null && await db.ProductSet.AnyAsync(x => x.Id != id && x.Barcode == barcode, ct)) throw new ArgumentException("Barcode already exists.");
    }
    private static Product ToEntity(ProductInput input, Guid tenantId, Guid id) { var p = new Product { Id = id, TenantId = tenantId }; Apply(p, input); return p; }
    private static void Apply(Product p, ProductInput i) { p.Sku = i.Sku.Trim().ToUpperInvariant(); p.Barcode = Normalize(i.Barcode); p.Name = new(i.NameDe, i.NameTr); p.Description = new(i.DescriptionDe ?? "", i.DescriptionTr ?? ""); p.Brand = Normalize(i.Brand); p.CategoryId = i.CategoryId; p.UnitId = i.UnitId; p.TaxClassCode = i.TaxClassCode.Trim(); p.PurchaseCost = i.PurchaseCost; p.SalesPrice = i.SalesPrice; p.MinimumStock = i.MinimumStock; p.ImageUrl = Normalize(i.ImageUrl); if (i is ProductUpdate u) p.IsActive = u.IsActive; }
    private static async Task ValidateUnit(UnitInput input, Guid? id, AlmerDbContext db, CancellationToken ct) { ValidateNames(input.NameDe, input.NameTr); if (string.IsNullOrWhiteSpace(input.Symbol) || input.ConversionFactor <= 0) throw new ArgumentException("Invalid unit."); if (input.BaseUnitId.HasValue && !await db.UnitOfMeasureSet.AnyAsync(x => x.Id == input.BaseUnitId, ct)) throw new ArgumentException("Base unit was not found."); if (id.HasValue) await EnsureNoUnitCycle(id.Value, input.BaseUnitId, db, ct); }
    private static async Task EnsureNoCategoryCycle(Guid id, Guid? parentId, AlmerDbContext db, CancellationToken ct) { var current = parentId; var seen = new HashSet<Guid>(); while (current.HasValue) { if (current == id || !seen.Add(current.Value)) throw new ArgumentException("Category hierarchy contains a cycle."); current = await db.ProductCategorySet.Where(x => x.Id == current).Select(x => x.ParentId).SingleOrDefaultAsync(ct); } }
    private static async Task EnsureNoUnitCycle(Guid id, Guid? baseId, AlmerDbContext db, CancellationToken ct) { var current = baseId; var seen = new HashSet<Guid>(); while (current.HasValue) { if (current == id || !seen.Add(current.Value)) throw new ArgumentException("Unit conversion contains a cycle."); current = await db.UnitOfMeasureSet.Where(x => x.Id == current).Select(x => x.BaseUnitId).SingleOrDefaultAsync(ct); } }
    private static void ValidateNames(string de, string tr) { if (string.IsNullOrWhiteSpace(de) || string.IsNullOrWhiteSpace(tr)) throw new ArgumentException("German and Turkish names are required."); }
    private static async Task ValidateStockReferences(Guid branchId, Guid productId, AlmerDbContext db, CancellationToken ct) { if (!await db.BranchSet.AnyAsync(x => x.Id == branchId && x.IsActive, ct) || !await db.ProductSet.AnyAsync(x => x.Id == productId && x.IsActive, ct)) throw new ArgumentException("Active branch or product was not found."); }
    private static async Task<decimal> CurrentStock(AlmerDbContext db, Guid branchId, Guid productId, CancellationToken ct) => await db.StockMovementSet.Where(x => x.BranchId == branchId && x.ProductId == productId).SumAsync(x => (decimal?)x.QuantityDelta, ct) ?? 0m;
    private static StockMovement NewMovement(ITenantContext c, Guid branch, Guid product, StockMovementType type, decimal delta, decimal? cost, string? reference, string? note, string? lot, DateOnly? expiry, Guid? transfer) => new() { Id = Guid.NewGuid(), TenantId = c.TenantId, BranchId = branch, ProductId = product, Type = type, QuantityDelta = delta, UnitCost = cost, Reference = Normalize(reference), Note = Normalize(note), LotNumber = Normalize(lot), ExpiresOn = expiry, TransferGroupId = transfer, CreatedByUserId = c.UserId, OccurredAt = DateTimeOffset.UtcNow };
    private static void EnsureBranch(ITenantContext context, Guid branchId) { if (context.AllowedBranchIds.Count > 0 && !context.AllowedBranchIds.Contains(branchId)) throw new UnauthorizedAccessException("Branch access denied."); }
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static object Map(ProductCategory x) => new { x.Id, x.ParentId, nameDe = x.Name.De, nameTr = x.Name.Tr, x.SortOrder, x.IsActive };
    private static object Map(UnitOfMeasure x) => new { x.Id, nameDe = x.Name.De, nameTr = x.Name.Tr, x.Symbol, x.BaseUnitId, x.ConversionFactor, x.IsActive };
    private static object Map(Product x) => new { x.Id, x.Sku, x.Barcode, nameDe = x.Name.De, nameTr = x.Name.Tr, descriptionDe = x.Description.De, descriptionTr = x.Description.Tr, x.Brand, x.CategoryId, x.UnitId, x.TaxClassCode, x.PurchaseCost, x.SalesPrice, x.MinimumStock, x.ImageUrl, x.IsActive };
}

public record CategoryInput(string NameDe, string NameTr, Guid? ParentId, int SortOrder);
public sealed record CategoryUpdate(string NameDe, string NameTr, Guid? ParentId, int SortOrder, bool IsActive) : CategoryInput(NameDe, NameTr, ParentId, SortOrder);
public record UnitInput(string NameDe, string NameTr, string Symbol, Guid? BaseUnitId, decimal ConversionFactor);
public sealed record UnitUpdate(string NameDe, string NameTr, string Symbol, Guid? BaseUnitId, decimal ConversionFactor, bool IsActive) : UnitInput(NameDe, NameTr, Symbol, BaseUnitId, ConversionFactor);
public record ProductInput(string Sku, string? Barcode, string NameDe, string NameTr, string? DescriptionDe, string? DescriptionTr, string? Brand, Guid CategoryId, Guid UnitId, string TaxClassCode, decimal PurchaseCost, decimal SalesPrice, decimal MinimumStock, string? ImageUrl);
public sealed record ProductUpdate(string Sku, string? Barcode, string NameDe, string NameTr, string? DescriptionDe, string? DescriptionTr, string? Brand, Guid CategoryId, Guid UnitId, string TaxClassCode, decimal PurchaseCost, decimal SalesPrice, decimal MinimumStock, string? ImageUrl, bool IsActive) : ProductInput(Sku, Barcode, NameDe, NameTr, DescriptionDe, DescriptionTr, Brand, CategoryId, UnitId, TaxClassCode, PurchaseCost, SalesPrice, MinimumStock, ImageUrl);
public sealed record BranchPriceInput(Guid BranchId, decimal Price);
public sealed record StockMovementInput(Guid BranchId, Guid ProductId, StockMovementType Type, decimal Quantity, decimal? UnitCost, string? Reference, string? Note, string? LotNumber, DateOnly? ExpiresOn);
public sealed record StockTransferInput(Guid FromBranchId, Guid ToBranchId, Guid ProductId, decimal Quantity, string? Reference, string? Note);
public sealed record StockBalanceDto(Guid BranchId, string BranchNameDe, string BranchNameTr, Guid ProductId, string Sku, string ProductNameDe, string ProductNameTr, decimal Quantity, decimal MinimumStock, bool IsCritical);
