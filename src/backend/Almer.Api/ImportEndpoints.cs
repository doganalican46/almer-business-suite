using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Almer.Application;
using Almer.Domain;
using Almer.Infrastructure;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace Almer.Api;

public static class ImportEndpoints
{
    private const int MaxFileBytes = 10 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Dictionary<ImportType, string[]> Fields = new()
    {
        [ImportType.Product] = ["sku", "barcode", "nameDe", "nameTr", "category", "unit", "taxClass", "purchaseCost", "salesPrice", "minimumStock"],
        [ImportType.Supplier] = ["code", "name", "taxNumber", "email", "phone", "address"],
        [ImportType.Sale] = ["transactionNumber", "occurredAt", "productSku", "quantity", "unitPrice", "discountAmount", "paymentMethod", "externalKey"],
        [ImportType.Stock] = ["externalKey", "occurredAt", "productSku", "quantity", "type", "unitCost", "lotNumber", "expiresOn", "note"],
        [ImportType.Expense] = ["externalKey", "date", "category", "netAmount", "taxRate", "description", "paymentMethod"],
        [ImportType.Purchase] = ["documentNumber", "date", "supplierCode", "productSku", "quantity", "unitCost", "externalKey"]
    };
    private static readonly Dictionary<string, string[]> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sku"] = ["sku", "artikelnummer", "ürünkodu", "urunkodu", "productcode"], ["barcode"] = ["barcode", "ean", "barkod"],
        ["nameDe"] = ["namede", "namedeutsch", "produktname", "adalmanca"], ["nameTr"] = ["nametr", "nametürkisch", "adtürkçe", "adtr"],
        ["category"] = ["category", "kategorie", "kategori"], ["unit"] = ["unit", "einheit", "birim"], ["taxClass"] = ["taxclass", "steuerklasse", "vergisınıfı", "vergisinifi"],
        ["purchaseCost"] = ["purchasecost", "einkaufspreis", "alışmaliyeti", "alismaliyeti"], ["salesPrice"] = ["salesprice", "verkaufspreis", "satışfiyatı", "satisfiyati"], ["minimumStock"] = ["minimumstock", "mindestbestand", "minimumstok"],
        ["code"] = ["code", "lieferantencode", "tedarikçikodu", "tedarikcikodu"], ["name"] = ["name", "ad", "lieferant"], ["taxNumber"] = ["taxnumber", "steuernummer", "verginumarası", "verginumarasi"],
        ["email"] = ["email", "e-mail"], ["phone"] = ["phone", "telefon"], ["address"] = ["address", "adresse", "adres"],
        ["transactionNumber"] = ["transactionnumber", "transaktion", "işlemno", "islemno"], ["documentNumber"] = ["documentnumber", "belegnummer", "belgeno"],
        ["occurredAt"] = ["occurredat", "datetime", "datum", "tarih"], ["date"] = ["date", "datum", "tarih"], ["productSku"] = ["productsku", "artikelnummer", "ürünkodu", "urunkodu", "sku"],
        ["quantity"] = ["quantity", "menge", "miktar"], ["unitPrice"] = ["unitprice", "einzelpreis", "birimfiyat"], ["discountAmount"] = ["discount", "rabatt", "indirim"],
        ["paymentMethod"] = ["paymentmethod", "zahlungsart", "ödeme", "odeme"], ["externalKey"] = ["externalkey", "recordid", "referenz", "referans"],
        ["type"] = ["type", "bewegungsart", "harekettürü", "hareketturu"], ["unitCost"] = ["unitcost", "stückkosten", "birimmaliyet"], ["lotNumber"] = ["lot", "charge", "parti"],
        ["expiresOn"] = ["expiry", "ablaufdatum", "sonkullanma"], ["note"] = ["note", "notiz", "not"],
        ["netAmount"] = ["netamount", "netto", "nettutar"], ["taxRate"] = ["taxrate", "steuersatz", "vergioranı", "vergiorani"], ["description"] = ["description", "beschreibung", "açıklama", "aciklama"],
        ["supplierCode"] = ["suppliercode", "lieferantencode", "tedarikçikodu", "tedarikcikodu"]
    };

    public static RouteGroupBuilder MapImportEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/imports", History).RequirePermission(PermissionCodes.ImportsRead);
        group.MapPost("/imports/upload", Upload).DisableAntiforgery().RequirePermission(PermissionCodes.ImportsCreate);
        group.MapPut("/imports/{id:guid}/validate", Validate).RequirePermission(PermissionCodes.ImportsCreate);
        group.MapPost("/imports/{id:guid}/commit", QueueCommit).RequirePermission(PermissionCodes.ImportsCreate);
        group.MapGet("/imports/template/{type}", DownloadTemplate).RequirePermission(PermissionCodes.ImportsRead);
        group.MapGet("/import-mapping-templates", Templates).RequirePermission(PermissionCodes.ImportsRead);
        group.MapPost("/import-mapping-templates", SaveTemplate).RequirePermission(PermissionCodes.ImportsManageTemplates);
        group.MapDelete("/import-mapping-templates/{id:guid}", DeleteTemplate).RequirePermission(PermissionCodes.ImportsManageTemplates);
        return group;
    }

    private static async Task<IResult> History(ITenantContext tenant, AlmerDbContext db, CancellationToken ct) { var query = db.ImportBatchSet.AsQueryable(); if (tenant.AllowedBranchIds.Count > 0) query = query.Where(x => x.BranchId.HasValue && tenant.AllowedBranchIds.Contains(x.BranchId.Value)); return Results.Ok(await query.OrderByDescending(x => x.CreatedAt).Take(100).Select(x => new { x.Id, x.BranchId, x.Type, x.Status, x.FileName, x.TotalRows, x.ValidRows, x.ErrorRows, x.ImportedRows, x.CreatedAt, x.CompletedAt, x.BackgroundJobId, x.FailureMessage }).ToListAsync(ct)); }

    private static async Task<IResult> Upload(IFormFile file, ImportType type, Guid? branchId, string? delimiter, string? encoding, string? decimalSeparator, string? dateFormat, ITenantContext tenant, AlmerDbContext db, CancellationToken ct)
    {
        if (file.Length is <= 0 or > MaxFileBytes) throw new ArgumentException("File size must be between 1 byte and 10 MB.");
        EnsureBranch(tenant, branchId); if (type is ImportType.Sale or ImportType.Stock or ImportType.Purchase && !branchId.HasValue) throw new ArgumentException("A branch is required for this import type.");
        if (branchId.HasValue && !await db.BranchSet.AnyAsync(x => x.Id == branchId && x.IsActive, ct)) throw new ArgumentException("Branch not found."); var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".csv" or ".xlsx" or ".xls")) throw new ArgumentException("Only XLSX, XLS and CSV files are supported.");
        await using var stream = new MemoryStream(); await file.CopyToAsync(stream, ct); var data = stream.ToArray(); var hash = Convert.ToHexString(SHA256.HashData(data));
        if (await db.ImportBatchSet.AnyAsync(x => x.Type == type && x.BranchId == branchId && x.FileHash == hash, ct)) throw new InvalidOperationException("This file was already uploaded for the selected type and branch.");
        var options = new ImportOptions(delimiter ?? ";", encoding ?? "utf-8", decimalSeparator ?? ",", dateFormat ?? "dd.MM.yyyy"); var table = ImportFileReader.Read(data, file.FileName, options); var mapping = SuggestMapping(type, table.Columns);
        var batch = new ImportBatch { Id = Guid.NewGuid(), TenantId = tenant.TenantId, BranchId = branchId, Type = type, FileName = Path.GetFileName(file.FileName), ContentType = file.ContentType ?? "application/octet-stream", FileHash = hash, SourceData = data, MappingJson = JsonSerializer.Serialize(mapping, JsonOptions), OptionsJson = JsonSerializer.Serialize(options, JsonOptions), TotalRows = table.Rows.Count, CreatedByUserId = tenant.UserId, CreatedAt = DateTimeOffset.UtcNow };
        db.Add(batch); await db.SaveChangesAsync(ct);
        return Results.Ok(new { batch.Id, batch.Type, batch.FileName, columns = table.Columns, fields = Fields[type], suggestedMapping = mapping, preview = table.Rows.Take(10), totalRows = table.Rows.Count });
    }

    private static async Task<IResult> Validate(Guid id, ImportValidationRequest request, ITenantContext tenant, AlmerDbContext db, CancellationToken ct)
    {
        var batch = await db.ImportBatchSet.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Import not found."); EnsureBranch(tenant, batch.BranchId);
        var result = await ValidateCore(batch, request, db, ct); batch.MappingJson = JsonSerializer.Serialize(request.Mapping, JsonOptions); batch.OptionsJson = JsonSerializer.Serialize(request.Options, JsonOptions); batch.ErrorsJson = JsonSerializer.Serialize(result.Errors, JsonOptions); batch.ValidRows = result.ValidRows; batch.ErrorRows = result.Errors.Select(x => x.Row).Distinct().Count(); batch.Status = batch.ErrorRows == 0 ? ImportStatus.Validated : ImportStatus.Uploaded;
        await db.SaveChangesAsync(ct); return Results.Ok(result);
    }

    private static async Task<IResult> QueueCommit(Guid id, ITenantContext tenant, AlmerDbContext db, CancellationToken ct)
    {
        var batch = await db.ImportBatchSet.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Import not found."); EnsureBranch(tenant, batch.BranchId);
        if (batch.Status == ImportStatus.Completed) throw new InvalidOperationException("Import was already completed.");
        if (batch.Status is ImportStatus.Queued or ImportStatus.Processing) throw new InvalidOperationException("Import is already queued.");
        var job = new BackgroundJob { Id = Guid.NewGuid(), TenantId = tenant.TenantId, Type = BackgroundJobType.ImportCommit, Status = BackgroundJobStatus.Queued, PayloadJson = JsonSerializer.Serialize(new ImportCommitPayload(batch.Id), JsonOptions), BranchIdsJson = JsonSerializer.Serialize(tenant.AllowedBranchIds, JsonOptions), CreatedByUserId = tenant.UserId, CreatedAt = DateTimeOffset.UtcNow, AvailableAt = DateTimeOffset.UtcNow };
        db.BackgroundJobSet.Add(job); batch.Status = ImportStatus.Queued; batch.BackgroundJobId = job.Id; batch.FailureMessage = null;
        await db.SaveChangesAsync(ct);
        return Results.Accepted($"/api/background-jobs/{job.Id}", new { jobId = job.Id, status = job.Status });
    }

    public static async Task<ImportCommitResult> CommitBatchAsync(Guid id, ITenantContext tenant, AlmerDbContext db, CancellationToken ct)
    {
        var batch = await db.ImportBatchSet.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Import not found."); EnsureBranch(tenant, batch.BranchId);
        if (batch.Status == ImportStatus.Completed) return new(batch.Id, batch.Status, batch.ImportedRows, batch.CompletedAt);
        batch.Status = ImportStatus.Processing; batch.FailureMessage = null; await db.SaveChangesAsync(ct);
        var request = new ImportValidationRequest(JsonSerializer.Deserialize<Dictionary<string, string>>(batch.MappingJson, JsonOptions) ?? [], JsonSerializer.Deserialize<ImportOptions>(batch.OptionsJson, JsonOptions) ?? new());
        var validation = await ValidateCore(batch, request, db, ct); if (validation.Errors.Count > 0) throw new InvalidOperationException("Validation errors must be corrected before import.");
        var table = ImportFileReader.Read(batch.SourceData, batch.FileName, request.Options); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var imported = await CommitRows(batch, table.Rows, request, tenant, db, ct); batch.Status = ImportStatus.Completed; batch.ImportedRows = imported; batch.ValidRows = validation.ValidRows; batch.ErrorRows = 0; batch.CompletedAt = DateTimeOffset.UtcNow; batch.SourceData = [];
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return new(batch.Id, batch.Status, batch.ImportedRows, batch.CompletedAt);
        }
        catch { await transaction.RollbackAsync(ct); throw; }
    }

    private static async Task<ImportValidationResult> ValidateCore(ImportBatch batch, ImportValidationRequest request, AlmerDbContext db, CancellationToken ct)
    {
        var table = ImportFileReader.Read(batch.SourceData, batch.FileName, request.Options); var errors = new List<ImportRowError>();
        foreach (var field in Required(batch.Type)) if (!request.Mapping.TryGetValue(field, out var column) || !table.Columns.Contains(column, StringComparer.OrdinalIgnoreCase)) errors.Add(new(1, field, "Required column is not mapped."));
        if (errors.Count > 0) return new(table.Rows.Count, 0, errors, table.Rows.Take(20).ToArray());
        var products = await db.ProductSet.Select(x => x.Sku).ToListAsync(ct); var suppliers = await db.SupplierSet.Select(x => x.Code).ToListAsync(ct); var categories = await db.ProductCategorySet.Select(x => new { x.Name.De, x.Name.Tr }).ToListAsync(ct); var units = await db.UnitOfMeasureSet.Select(x => new { x.Name.De, x.Name.Tr, x.Symbol }).ToListAsync(ct); var financialCategories = await db.FinancialCategorySet.Where(x => x.Type == FinancialEntryType.Expense).Select(x => new { x.Name.De, x.Name.Tr }).ToListAsync(ct);
        var knownKeys = await db.ImportedRecordKeySet.Where(x => x.Type == batch.Type).Select(x => x.ExternalKey).ToListAsync(ct);
        if (batch.Type == ImportType.Sale) knownKeys.AddRange(await db.SaleSet.Select(x => x.TransactionNumber).ToListAsync(ct));
        if (batch.Type == ImportType.Purchase) knownKeys.AddRange(await db.PurchaseSet.Select(x => x.DocumentNumber).ToListAsync(ct));
        var fileKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < table.Rows.Count; i++)
        {
            var row = table.Rows[i]; var number = i + 2; string V(string field) => request.Mapping.TryGetValue(field, out var c) && row.TryGetValue(c, out var v) ? v.Trim() : "";
            void Need(string field) { if (string.IsNullOrWhiteSpace(V(field))) errors.Add(new(number, field, "Value is required.")); }
            foreach (var field in Required(batch.Type)) Need(field);
            foreach (var field in DecimalFields(batch.Type)) if (V(field).Length > 0 && (!ImportFileReader.TryDecimal(V(field), request.Options, out var n) || n < 0)) errors.Add(new(number, field, "Invalid non-negative number."));
            foreach (var field in DateFields(batch.Type)) if (V(field).Length > 0 && !ImportFileReader.TryDate(V(field), request.Options, out _)) errors.Add(new(number, field, "Invalid date."));
            var key = ExternalKey(batch.Type, V); var repeatsAreLines = batch.Type is ImportType.Sale or ImportType.Purchase;
            if (key.Length > 0 && ((!repeatsAreLines && !fileKeys.Add(key)) || knownKeys.Contains(key, StringComparer.OrdinalIgnoreCase))) errors.Add(new(number, "externalKey", "Duplicate record."));
            if (repeatsAreLines) fileKeys.Add(key);
            if (batch.Type == ImportType.Product && products.Contains(V("sku"), StringComparer.OrdinalIgnoreCase)) errors.Add(new(number, "sku", "Product SKU already exists."));
            if (batch.Type == ImportType.Supplier && suppliers.Contains(V("code"), StringComparer.OrdinalIgnoreCase)) errors.Add(new(number, "code", "Supplier code already exists."));
            if (batch.Type is ImportType.Sale or ImportType.Stock or ImportType.Purchase && V("productSku").Length > 0 && !products.Contains(V("productSku"), StringComparer.OrdinalIgnoreCase)) errors.Add(new(number, "productSku", "Product SKU was not found."));
            if (batch.Type == ImportType.Purchase && V("supplierCode").Length > 0 && !suppliers.Contains(V("supplierCode"), StringComparer.OrdinalIgnoreCase)) errors.Add(new(number, "supplierCode", "Supplier code was not found."));
            if (batch.Type == ImportType.Product && V("category").Length > 0 && !categories.Any(x => Eq(x.De, V("category")) || Eq(x.Tr, V("category")))) errors.Add(new(number, "category", "Category was not found."));
            if (batch.Type == ImportType.Product && V("unit").Length > 0 && !units.Any(x => Eq(x.De, V("unit")) || Eq(x.Tr, V("unit")) || Eq(x.Symbol, V("unit")))) errors.Add(new(number, "unit", "Unit was not found."));
            if (batch.Type == ImportType.Expense && V("category").Length > 0 && !financialCategories.Any(x => Eq(x.De, V("category")) || Eq(x.Tr, V("category")))) errors.Add(new(number, "category", "Expense category was not found."));
            if (batch.Type == ImportType.Stock && V("type").Length > 0 && !Enum.TryParse<StockMovementType>(V("type"), true, out _)) errors.Add(new(number, "type", "Unknown movement type."));
        }
        var invalid = errors.Where(x => x.Row > 1).Select(x => x.Row).Distinct().Count(); return new(table.Rows.Count, table.Rows.Count - invalid, errors, table.Rows.Take(20).ToArray());
    }

    private static async Task<int> CommitRows(ImportBatch batch, IReadOnlyList<IReadOnlyDictionary<string, string>> rows, ImportValidationRequest request, ITenantContext tenant, AlmerDbContext db, CancellationToken ct)
    {
        string V(IReadOnlyDictionary<string, string> row, string field) => request.Mapping.TryGetValue(field, out var c) && row.TryGetValue(c, out var v) ? v.Trim() : "";
        decimal D(IReadOnlyDictionary<string, string> row, string field) { ImportFileReader.TryDecimal(V(row, field), request.Options, out var value); return value; }
        DateTimeOffset Date(IReadOnlyDictionary<string, string> row, string field) { ImportFileReader.TryDate(V(row, field), request.Options, out var value); return value; }
        var products = await db.ProductSet.ToDictionaryAsync(x => x.Sku, StringComparer.OrdinalIgnoreCase, ct); var imported = 0;
        if (batch.Type == ImportType.Product)
        {
            var categories = await db.ProductCategorySet.ToListAsync(ct); var units = await db.UnitOfMeasureSet.ToListAsync(ct);
            foreach (var row in rows) { var sku = V(row, "sku").ToUpperInvariant(); var entity = new Product { Id = Guid.NewGuid(), TenantId = tenant.TenantId, Sku = sku, Barcode = Clean(V(row, "barcode")), Name = new(V(row, "nameDe"), V(row, "nameTr")), Description = new("", ""), CategoryId = categories.Single(x => Eq(x.Name.De, V(row, "category")) || Eq(x.Name.Tr, V(row, "category"))).Id, UnitId = units.Single(x => Eq(x.Name.De, V(row, "unit")) || Eq(x.Name.Tr, V(row, "unit")) || Eq(x.Symbol, V(row, "unit"))).Id, TaxClassCode = V(row, "taxClass"), PurchaseCost = D(row, "purchaseCost"), SalesPrice = D(row, "salesPrice"), MinimumStock = D(row, "minimumStock") }; db.ProductSet.Add(entity); AddKey(batch, sku, db); imported++; }
        }
        else if (batch.Type == ImportType.Supplier)
        {
            foreach (var row in rows) { var code = V(row, "code").ToUpperInvariant(); db.SupplierSet.Add(new Supplier { Id = Guid.NewGuid(), TenantId = tenant.TenantId, Code = code, Name = V(row, "name"), TaxNumber = Clean(V(row, "taxNumber")), Email = Clean(V(row, "email")), Phone = Clean(V(row, "phone")), Address = Clean(V(row, "address")) }); AddKey(batch, code, db); imported++; }
        }
        else if (batch.Type == ImportType.Stock)
        {
            var branchId = batch.BranchId!.Value; foreach (var row in rows) { var key = ExternalKey(batch.Type, f => V(row, f)); var type = Enum.Parse<StockMovementType>(V(row, "type"), true); var quantity = D(row, "quantity") * (type is StockMovementType.Sale or StockMovementType.SupplierReturn or StockMovementType.Waste or StockMovementType.TransferOut ? -1 : 1); db.StockMovementSet.Add(new StockMovement { Id = Guid.NewGuid(), TenantId = tenant.TenantId, BranchId = branchId, ProductId = products[V(row, "productSku")].Id, Type = type, QuantityDelta = quantity, UnitCost = D(row, "unitCost"), Reference = key, Note = Clean(V(row, "note")), LotNumber = Clean(V(row, "lotNumber")), ExpiresOn = V(row, "expiresOn").Length == 0 ? null : DateOnly.FromDateTime(Date(row, "expiresOn").Date), CreatedByUserId = tenant.UserId, OccurredAt = Date(row, "occurredAt") }); AddKey(batch, key, db); imported++; }
        }
        else if (batch.Type == ImportType.Expense)
        {
            var categories = await db.FinancialCategorySet.Where(x => x.Type == FinancialEntryType.Expense).ToListAsync(ct); foreach (var row in rows) { var key = ExternalKey(batch.Type, f => V(row, f)); var net = D(row, "netAmount"); var rate = D(row, "taxRate"); var tax = TaxCalculator.FromNet(net, rate).Tax; db.FinancialEntrySet.Add(new FinancialEntry { Id = Guid.NewGuid(), TenantId = tenant.TenantId, BranchId = batch.BranchId, CategoryId = categories.Single(x => Eq(x.Name.De, V(row, "category")) || Eq(x.Name.Tr, V(row, "category"))).Id, Type = FinancialEntryType.Expense, Date = DateOnly.FromDateTime(Date(row, "date").Date), NetAmount = net, TaxRate = rate, TaxAmount = tax, GrossAmount = net + tax, Description = V(row, "description"), PaymentMethod = V(row, "paymentMethod"), CreatedByUserId = tenant.UserId, CreatedAt = DateTimeOffset.UtcNow }); AddKey(batch, key, db); imported++; }
        }
        else if (batch.Type == ImportType.Sale)
        {
            var branchId = batch.BranchId!.Value; var taxRules = await db.TaxRuleSet.Where(x => x.IsActive).ToListAsync(ct); var currentStock = await db.StockMovementSet.Where(x => x.BranchId == branchId).GroupBy(x => x.ProductId).Select(x => new { x.Key, Quantity = x.Sum(y => y.QuantityDelta) }).ToDictionaryAsync(x => x.Key, x => x.Quantity, ct);
            foreach (var group in rows.GroupBy(x => V(x, "transactionNumber"), StringComparer.OrdinalIgnoreCase)) { var first = group.First(); var sale = new Sale { Id = Guid.NewGuid(), TenantId = tenant.TenantId, BranchId = branchId, TransactionNumber = group.Key, OccurredAt = Date(first, "occurredAt"), PaymentMethod = V(first, "paymentMethod"), SourceSystem = "Import", CreatedByUserId = tenant.UserId }; foreach (var row in group) { var product = products[V(row, "productSku")]; var quantity = D(row, "quantity"); currentStock[product.Id] = currentStock.GetValueOrDefault(product.Id) - quantity; if (currentStock[product.Id] < 0) throw new InvalidOperationException($"Insufficient stock for {product.Sku}."); var price = D(row, "unitPrice"); var discount = D(row, "discountAmount"); var net = decimal.Round(quantity * price - discount, 2); var taxRule = taxRules.Where(x => x.Code == product.TaxClassCode && x.ValidFrom <= DateOnly.FromDateTime(sale.OccurredAt.Date) && (!x.ValidTo.HasValue || x.ValidTo >= DateOnly.FromDateTime(sale.OccurredAt.Date))).OrderByDescending(x => x.ValidFrom).First(); var tax = TaxCalculator.FromNet(net, taxRule.Rate).Tax; sale.Lines.Add(new SaleLine { Id = Guid.NewGuid(), TenantId = tenant.TenantId, SaleId = sale.Id, ProductId = product.Id, ProductNameSnapshot = product.Name.De, Quantity = quantity, UnitPrice = price, UnitCost = product.PurchaseCost, DiscountAmount = discount, TaxRate = taxRule.Rate, NetAmount = net, TaxAmount = tax, GrossAmount = net + tax }); db.StockMovementSet.Add(new StockMovement { Id = Guid.NewGuid(), TenantId = tenant.TenantId, BranchId = branchId, ProductId = product.Id, Type = StockMovementType.Sale, QuantityDelta = -quantity, UnitCost = product.PurchaseCost, Reference = group.Key, CreatedByUserId = tenant.UserId, OccurredAt = sale.OccurredAt }); } sale.NetAmount = sale.Lines.Sum(x => x.NetAmount); sale.TaxAmount = sale.Lines.Sum(x => x.TaxAmount); sale.GrossAmount = sale.Lines.Sum(x => x.GrossAmount); sale.DiscountAmount = sale.Lines.Sum(x => x.DiscountAmount); db.SaleSet.Add(sale); AddKey(batch, group.Key, db); imported += group.Count(); }
        }
        else if (batch.Type == ImportType.Purchase)
        {
            var branchId = batch.BranchId!.Value; var suppliers = await db.SupplierSet.ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, ct); var taxRules = await db.TaxRuleSet.Where(x => x.IsActive).ToListAsync(ct);
            foreach (var group in rows.GroupBy(x => V(x, "documentNumber"), StringComparer.OrdinalIgnoreCase)) { var first = group.First(); var date = DateOnly.FromDateTime(Date(first, "date").Date); var purchase = new Purchase { Id = Guid.NewGuid(), TenantId = tenant.TenantId, BranchId = branchId, SupplierId = suppliers[V(first, "supplierCode")].Id, DocumentNumber = group.Key, Date = date, CreatedByUserId = tenant.UserId, CreatedAt = DateTimeOffset.UtcNow }; foreach (var row in group) { var product = products[V(row, "productSku")]; var quantity = D(row, "quantity"); var cost = D(row, "unitCost"); var net = decimal.Round(quantity * cost, 2); var rate = taxRules.Where(x => x.Code == product.TaxClassCode && x.ValidFrom <= date && (!x.ValidTo.HasValue || x.ValidTo >= date)).OrderByDescending(x => x.ValidFrom).First().Rate; var tax = TaxCalculator.FromNet(net, rate).Tax; purchase.Lines.Add(new PurchaseLine { Id = Guid.NewGuid(), TenantId = tenant.TenantId, PurchaseId = purchase.Id, ProductId = product.Id, ProductNameSnapshot = product.Name.De, Quantity = quantity, UnitCost = cost, TaxRate = rate, NetAmount = net, TaxAmount = tax, GrossAmount = net + tax }); db.StockMovementSet.Add(new StockMovement { Id = Guid.NewGuid(), TenantId = tenant.TenantId, BranchId = branchId, ProductId = product.Id, Type = StockMovementType.Purchase, QuantityDelta = quantity, UnitCost = cost, Reference = group.Key, CreatedByUserId = tenant.UserId, OccurredAt = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) }); product.PurchaseCost = cost; } purchase.NetAmount = purchase.Lines.Sum(x => x.NetAmount); purchase.TaxAmount = purchase.Lines.Sum(x => x.TaxAmount); purchase.GrossAmount = purchase.Lines.Sum(x => x.GrossAmount); db.PurchaseSet.Add(purchase); AddKey(batch, group.Key, db); imported += group.Count(); }
        }
        return imported;
    }

    private static IResult DownloadTemplate(ImportType type, string format = "xlsx")
    {
        var fields = Fields[type]; if (format.Equals("csv", StringComparison.OrdinalIgnoreCase)) return Results.File(Encoding.UTF8.GetBytes(string.Join(';', fields) + Environment.NewLine), "text/csv; charset=utf-8", $"almer-{type.ToString().ToLowerInvariant()}-template.csv");
        using var workbook = new XLWorkbook(); var sheet = workbook.AddWorksheet(type.ToString()); for (var i = 0; i < fields.Length; i++) { sheet.Cell(1, i + 1).Value = fields[i]; sheet.Cell(1, i + 1).Style.Font.Bold = true; sheet.Column(i + 1).Width = Math.Max(14, fields[i].Length + 3); } sheet.SheetView.FreezeRows(1); using var stream = new MemoryStream(); workbook.SaveAs(stream); return Results.File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"almer-{type.ToString().ToLowerInvariant()}-template.xlsx");
    }

    private static async Task<IResult> Templates(ImportType? type, AlmerDbContext db, CancellationToken ct) { var query = db.ImportMappingTemplateSet.AsQueryable(); if (type.HasValue) query = query.Where(x => x.Type == type); return Results.Ok(await query.OrderBy(x => x.Name).ToListAsync(ct)); }
    private static async Task<IResult> SaveTemplate(ImportTemplateInput input, ITenantContext tenant, AlmerDbContext db, CancellationToken ct) { if (string.IsNullOrWhiteSpace(input.Name)) throw new ArgumentException("Template name is required."); var entity = new ImportMappingTemplate { Id = Guid.NewGuid(), TenantId = tenant.TenantId, Name = input.Name.Trim(), Type = input.Type, MappingJson = JsonSerializer.Serialize(input.Mapping, JsonOptions), OptionsJson = JsonSerializer.Serialize(input.Options, JsonOptions), CreatedAt = DateTimeOffset.UtcNow }; db.Add(entity); await db.SaveChangesAsync(ct); return Results.Ok(entity); }
    private static async Task<IResult> DeleteTemplate(Guid id, AlmerDbContext db, CancellationToken ct) { var entity = await db.ImportMappingTemplateSet.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Template not found."); db.Remove(entity); await db.SaveChangesAsync(ct); return Results.NoContent(); }
    private static Dictionary<string, string> SuggestMapping(ImportType type, IReadOnlyList<string> columns) => Fields[type].Select(field => new { field, column = columns.FirstOrDefault(c => Aliases.GetValueOrDefault(field, [field]).Any(a => Normalize(a) == Normalize(c))) }).Where(x => x.column is not null).ToDictionary(x => x.field, x => x.column!);
    private static string Normalize(string value) => Regex.Replace(value.ToLowerInvariant().Normalize(NormalizationForm.FormD), "[^a-z0-9]", "");
    private static string[] Required(ImportType type) => type switch { ImportType.Product => ["sku", "nameDe", "category", "unit", "taxClass", "purchaseCost", "salesPrice"], ImportType.Supplier => ["code", "name"], ImportType.Sale => ["transactionNumber", "occurredAt", "productSku", "quantity", "unitPrice", "paymentMethod"], ImportType.Stock => ["externalKey", "occurredAt", "productSku", "quantity", "type"], ImportType.Expense => ["externalKey", "date", "category", "netAmount", "taxRate", "description", "paymentMethod"], ImportType.Purchase => ["documentNumber", "date", "supplierCode", "productSku", "quantity", "unitCost"], _ => [] };
    private static string[] DecimalFields(ImportType type) => type switch { ImportType.Product => ["purchaseCost", "salesPrice", "minimumStock"], ImportType.Sale => ["quantity", "unitPrice", "discountAmount"], ImportType.Stock => ["quantity", "unitCost"], ImportType.Expense => ["netAmount", "taxRate"], ImportType.Purchase => ["quantity", "unitCost"], _ => [] };
    private static string[] DateFields(ImportType type) => type switch { ImportType.Sale => ["occurredAt"], ImportType.Stock => ["occurredAt", "expiresOn"], ImportType.Expense or ImportType.Purchase => ["date"], _ => [] };
    private static string ExternalKey(ImportType type, Func<string, string> value) => type switch { ImportType.Product => value("sku"), ImportType.Supplier => value("code"), ImportType.Sale => value("transactionNumber"), ImportType.Stock or ImportType.Expense => value("externalKey"), ImportType.Purchase => value("documentNumber"), _ => "" };
    private static void AddKey(ImportBatch batch, string key, AlmerDbContext db) => db.ImportedRecordKeySet.Add(new ImportedRecordKey { Id = Guid.NewGuid(), TenantId = batch.TenantId, Type = batch.Type, ExternalKey = key.Trim().ToUpperInvariant(), ImportBatchId = batch.Id });
    private static void EnsureBranch(ITenantContext tenant, Guid? branchId) { if (tenant.AllowedBranchIds.Count > 0 && (!branchId.HasValue || !tenant.AllowedBranchIds.Contains(branchId.Value))) throw new UnauthorizedAccessException("Company-wide or unauthorized branch import denied."); }
    private static bool Eq(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
    private static string? Clean(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record ImportValidationRequest(Dictionary<string, string> Mapping, ImportOptions Options);
public sealed record ImportCommitPayload(Guid ImportBatchId);
public sealed record ImportCommitResult(Guid Id, ImportStatus Status, int ImportedRows, DateTimeOffset? CompletedAt);
public sealed record ImportRowError(int Row, string Field, string Message);
public sealed record ImportValidationResult(int TotalRows, int ValidRows, IReadOnlyList<ImportRowError> Errors, IReadOnlyList<IReadOnlyDictionary<string, string>> Preview);
public sealed record ImportTemplateInput(string Name, ImportType Type, Dictionary<string, string> Mapping, ImportOptions Options);
