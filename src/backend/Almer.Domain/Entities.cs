namespace Almer.Domain;

public interface ITenantEntity { Guid TenantId { get; set; } }

public sealed class LocalizedText
{
    public string De { get; private set; } = "";
    public string Tr { get; private set; } = "";
    private LocalizedText() { }
    public LocalizedText(string de, string tr) => (De, Tr) = (de.Trim(), tr.Trim());
    public string For(string locale) => locale.StartsWith("tr", StringComparison.OrdinalIgnoreCase) && Tr.Length > 0 ? Tr : De;
}

public sealed class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string DefaultLocale { get; set; } = "de-DE";
    public string Currency { get; set; } = "EUR";
    public bool IsDemo { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class LegalEntity : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public LocalizedText Name { get; set; } = new("", "");
    public string TradeName { get; set; } = "";
    public string TaxNumber { get; set; } = "";
    public string CountryCode { get; set; } = "DE";
    public bool IsActive { get; set; } = true;
}

public sealed class Branch : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid LegalEntityId { get; set; }
    public LocalizedText Name { get; set; } = new("", "");
    public string Code { get; set; } = "";
    public string Address { get; set; } = "";
    public string? ManagerName { get; set; }
    public string? Phone { get; set; }
    public DateOnly? OpenedOn { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string PreferredLocale { get; set; } = "de-DE";
    public string PreferredTheme { get; set; } = "light";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? EmailVerifiedAt { get; set; }
    public bool MfaEnabled { get; set; }
    public string? MfaSecretProtected { get; set; }
    public string? MfaPendingSecretProtected { get; set; }
    public string RecoveryCodesJson { get; set; } = "[]";
    public List<UserTenant> Tenants { get; set; } = [];
}

public sealed class UserTenant : ITenantEntity
{
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public Guid RoleId { get; set; }
    public bool IsActive { get; set; } = true;
    public User User { get; set; } = null!;
}

public sealed class UserSession : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid FamilyId { get; set; }
    public string RefreshTokenHash { get; set; } = "";
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastUsedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? RevokeReason { get; set; }
    public Guid? ReplacedBySessionId { get; set; }
}

public sealed class UserBranch : ITenantEntity
{
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
}

public sealed class Role : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public LocalizedText Name { get; set; } = new("", "");
    public bool IsSystem { get; set; }
    public List<RolePermission> Permissions { get; set; } = [];
}

public sealed class RolePermission : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid RoleId { get; set; }
    public string PermissionCode { get; set; } = "";
}

public sealed class DailyMetric : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public DateOnly Date { get; set; }
    public decimal GrossSales { get; set; }
    public decimal Vat { get; set; }
    public decimal Returns { get; set; }
    public decimal Discounts { get; set; }
    public decimal CostOfGoodsSold { get; set; }
    public decimal OperatingExpenses { get; set; }
    public int TransactionCount { get; set; }
    public int SalesLines { get; set; }
    public int CostMatchedLines { get; set; }
}

public sealed class AuditEntry : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string EntityId { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
}

public sealed class ProductCategory : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? ParentId { get; set; }
    public LocalizedText Name { get; set; } = new("", "");
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class UnitOfMeasure : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public LocalizedText Name { get; set; } = new("", "");
    public string Symbol { get; set; } = "";
    public Guid? BaseUnitId { get; set; }
    public decimal ConversionFactor { get; set; } = 1m;
    public bool IsActive { get; set; } = true;
}

public sealed class Product : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Sku { get; set; } = "";
    public string? Barcode { get; set; }
    public LocalizedText Name { get; set; } = new("", "");
    public LocalizedText Description { get; set; } = new("", "");
    public string? Brand { get; set; }
    public Guid CategoryId { get; set; }
    public Guid UnitId { get; set; }
    public string TaxClassCode { get; set; } = "DE-7";
    public decimal PurchaseCost { get; set; }
    public decimal SalesPrice { get; set; }
    public decimal MinimumStock { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class ProductPrice : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? BranchId { get; set; }
    public decimal Price { get; set; }
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
}

public enum StockMovementType { Opening, Purchase, Sale, CustomerReturn, SupplierReturn, Waste, CountAdjustment, TransferOut, TransferIn }

public sealed class StockMovement : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid ProductId { get; set; }
    public StockMovementType Type { get; set; }
    public decimal QuantityDelta { get; set; }
    public decimal? UnitCost { get; set; }
    public string? Reference { get; set; }
    public string? Note { get; set; }
    public string? LotNumber { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public Guid? TransferGroupId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}

public sealed class TaxRule : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Code { get; set; } = "";
    public string CountryCode { get; set; } = "DE";
    public LocalizedText Name { get; set; } = new("", "");
    public decimal Rate { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Supplier : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? TaxNumber { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Purchase : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid SupplierId { get; set; }
    public string DocumentNumber { get; set; } = "";
    public DateOnly Date { get; set; }
    public decimal NetAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrossAmount { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<PurchaseLine> Lines { get; set; } = [];
}

public sealed class PurchaseLine : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PurchaseId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductNameSnapshot { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TaxRate { get; set; }
    public decimal NetAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrossAmount { get; set; }
}

public sealed class Sale : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public string TransactionNumber { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public string SourceSystem { get; set; } = "Manual";
    public bool IsReturn { get; set; }
    public decimal NetAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public Guid CreatedByUserId { get; set; }
    public List<SaleLine> Lines { get; set; } = [];
}

public sealed class SaleLine : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SaleId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductNameSnapshot { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal UnitCost { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxRate { get; set; }
    public decimal NetAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrossAmount { get; set; }
}

public enum FinancialEntryType { Income, Expense }
public sealed class FinancialCategory : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public FinancialEntryType Type { get; set; }
    public LocalizedText Name { get; set; } = new("", "");
    public bool IsActive { get; set; } = true;
}
public sealed class FinancialEntry : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid CategoryId { get; set; }
    public FinancialEntryType Type { get; set; }
    public DateOnly Date { get; set; }
    public decimal NetAmount { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrossAmount { get; set; }
    public string Description { get; set; } = "";
    public string PaymentMethod { get; set; } = "";
    public bool IsRecurring { get; set; }
    public bool IsApproved { get; set; }
    public string? DocumentUrl { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public enum CampaignType { Percentage, FixedAmount }
public sealed class Campaign : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public LocalizedText Name { get; set; } = new("", "");
    public CampaignType Type { get; set; }
    public decimal Value { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? BranchId { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
}

public enum ImportType { Product, Sale, Stock, Expense, Supplier, Purchase }
public enum ImportStatus { Uploaded = 0, Validated = 1, Completed = 2, Failed = 3, Queued = 4, Processing = 5 }

public sealed class ImportBatch : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public ImportType Type { get; set; }
    public ImportStatus Status { get; set; } = ImportStatus.Uploaded;
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string FileHash { get; set; } = "";
    public byte[] SourceData { get; set; } = [];
    public string MappingJson { get; set; } = "{}";
    public string OptionsJson { get; set; } = "{}";
    public string ErrorsJson { get; set; } = "[]";
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int ErrorRows { get; set; }
    public int ImportedRows { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public Guid? BackgroundJobId { get; set; }
    public string? FailureMessage { get; set; }
}

public enum BackgroundJobType { ImportCommit, NotificationRefresh, NotificationEmailDelivery, NotificationWebhookDelivery, AccountEmailDelivery }
public enum BackgroundJobStatus { Queued, Running, Succeeded, Failed, Cancelled }

public sealed class BackgroundJob : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public BackgroundJobType Type { get; set; }
    public BackgroundJobStatus Status { get; set; } = BackgroundJobStatus.Queued;
    public string PayloadJson { get; set; } = "{}";
    public string BranchIdsJson { get; set; } = "[]";
    public int Progress { get; set; }
    public int Attempts { get; set; }
    public int MaxAttempts { get; set; } = 3;
    public string? Error { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset AvailableAt { get; set; }
    public DateTimeOffset? LockedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class ImportMappingTemplate : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public ImportType Type { get; set; }
    public string MappingJson { get; set; } = "{}";
    public string OptionsJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ImportedRecordKey : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public ImportType Type { get; set; }
    public string ExternalKey { get; set; } = "";
    public Guid ImportBatchId { get; set; }
}

public sealed class TenantSetting : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string? LogoUrl { get; set; }
    public string PrimaryColor { get; set; } = "#147A46";
    public string EnabledModulesJson { get; set; } = "[]";
    public int CriticalStockThreshold { get; set; } = 10;
    public int ExpiryWarningDays { get; set; } = 14;
}

public sealed class DashboardPreference : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string HiddenWidgetsJson { get; set; } = "[]";
    public string WidgetOrderJson { get; set; } = "[]";
}

public enum NotificationType { CriticalStock, ExpiringStock, System }
public enum NotificationSeverity { Info, Warning, Critical }

public sealed class Notification : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? ProductId { get; set; }
    public string ExternalKey { get; set; } = "";
    public NotificationType Type { get; set; }
    public NotificationSeverity Severity { get; set; }
    public LocalizedText Title { get; set; } = new("", "");
    public LocalizedText Message { get; set; } = new("", "");
    public string? TargetUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
}

public sealed class NotificationRead : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid NotificationId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset ReadAt { get; set; }
}

public sealed class NotificationChannelSetting : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public bool EmailEnabled { get; set; }
    public string EmailRecipientsJson { get; set; } = "[]";
    public bool WebhookEnabled { get; set; }
    public string? WebhookUrl { get; set; }
    public string? WebhookSecretProtected { get; set; }
}

public enum AccountTokenType { PasswordReset, EmailVerification }

public sealed class AccountToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public AccountTokenType Type { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
}

public static class PermissionCodes
{
    public const string DashboardRead = "dashboard.read";
    public const string CompaniesManage = "companies.manage";
    public const string BranchesManage = "branches.manage";
    public const string RolesManage = "roles.manage";
    public const string UsersManage = "users.manage";
    public const string AuditRead = "audit.read";
    public const string ProductsRead = "products.read";
    public const string ProductsManage = "products.manage";
    public const string InventoryRead = "inventory.read";
    public const string InventoryAdjust = "inventory.adjust";
    public const string InventoryTransfer = "inventory.transfer";
    public const string SalesRead = "sales.read";
    public const string SalesCreate = "sales.create";
    public const string SuppliersManage = "suppliers.manage";
    public const string PurchasesManage = "purchases.manage";
    public const string FinanceRead = "finance.read";
    public const string FinanceManage = "finance.manage";
    public const string CampaignsManage = "campaigns.manage";
    public const string ImportsRead = "imports.read";
    public const string ImportsCreate = "imports.create";
    public const string ImportsManageTemplates = "imports.templates.manage";
    public const string ReportsRead = "reports.read";
    public const string ReportsExport = "reports.export";
    public const string SettingsManage = "settings.manage";
    public static readonly string[] All = [DashboardRead, CompaniesManage, BranchesManage, RolesManage, UsersManage, AuditRead, ProductsRead, ProductsManage, InventoryRead, InventoryAdjust, InventoryTransfer, SalesRead, SalesCreate, SuppliersManage, PurchasesManage, FinanceRead, FinanceManage, CampaignsManage, ImportsRead, ImportsCreate, ImportsManageTemplates, ReportsRead, ReportsExport, SettingsManage];
}
