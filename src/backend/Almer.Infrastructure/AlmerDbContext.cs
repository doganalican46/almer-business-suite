using Almer.Application;
using Almer.Domain;
using Microsoft.EntityFrameworkCore;

namespace Almer.Infrastructure;

public sealed class AlmerDbContext(DbContextOptions<AlmerDbContext> options, ITenantContext tenantContext) : DbContext(options), IAlmerData
{
    public bool AllowDevelopmentSeed { private get; set; }
    private Guid CurrentTenantId => tenantContext.TenantId;
    public DbSet<Tenant> TenantSet => Set<Tenant>();
    public DbSet<LegalEntity> LegalEntitySet => Set<LegalEntity>();
    public DbSet<Branch> BranchSet => Set<Branch>();
    public DbSet<User> UserSet => Set<User>();
    public DbSet<UserTenant> UserTenantSet => Set<UserTenant>();
    public DbSet<UserBranch> UserBranchSet => Set<UserBranch>();
    public DbSet<UserSession> UserSessionSet => Set<UserSession>();
    public DbSet<Role> RoleSet => Set<Role>();
    public DbSet<RolePermission> RolePermissionSet => Set<RolePermission>();
    public DbSet<DailyMetric> DailyMetricSet => Set<DailyMetric>();
    public DbSet<AuditEntry> AuditEntrySet => Set<AuditEntry>();
    public DbSet<ProductCategory> ProductCategorySet => Set<ProductCategory>();
    public DbSet<UnitOfMeasure> UnitOfMeasureSet => Set<UnitOfMeasure>();
    public DbSet<Product> ProductSet => Set<Product>();
    public DbSet<ProductPrice> ProductPriceSet => Set<ProductPrice>();
    public DbSet<StockMovement> StockMovementSet => Set<StockMovement>();
    public DbSet<TaxRule> TaxRuleSet => Set<TaxRule>();
    public DbSet<Supplier> SupplierSet => Set<Supplier>();
    public DbSet<Purchase> PurchaseSet => Set<Purchase>();
    public DbSet<PurchaseLine> PurchaseLineSet => Set<PurchaseLine>();
    public DbSet<Sale> SaleSet => Set<Sale>();
    public DbSet<SaleLine> SaleLineSet => Set<SaleLine>();
    public DbSet<FinancialCategory> FinancialCategorySet => Set<FinancialCategory>();
    public DbSet<FinancialEntry> FinancialEntrySet => Set<FinancialEntry>();
    public DbSet<Campaign> CampaignSet => Set<Campaign>();
    public DbSet<ImportBatch> ImportBatchSet => Set<ImportBatch>();
    public DbSet<ImportMappingTemplate> ImportMappingTemplateSet => Set<ImportMappingTemplate>();
    public DbSet<ImportedRecordKey> ImportedRecordKeySet => Set<ImportedRecordKey>();
    public DbSet<TenantSetting> TenantSettingSet => Set<TenantSetting>();
    public DbSet<DashboardPreference> DashboardPreferenceSet => Set<DashboardPreference>();
    public DbSet<Notification> NotificationSet => Set<Notification>();
    public DbSet<NotificationRead> NotificationReadSet => Set<NotificationRead>();
    public DbSet<NotificationChannelSetting> NotificationChannelSettingSet => Set<NotificationChannelSetting>();
    public DbSet<AccountToken> AccountTokenSet => Set<AccountToken>();
    public DbSet<BackgroundJob> BackgroundJobSet => Set<BackgroundJob>();

    IQueryable<Tenant> IAlmerData.Tenants => TenantSet;
    IQueryable<LegalEntity> IAlmerData.LegalEntities => LegalEntitySet;
    IQueryable<Branch> IAlmerData.Branches => BranchSet;
    IQueryable<DailyMetric> IAlmerData.DailyMetrics => DailyMetricSet;
    IQueryable<Role> IAlmerData.Roles => RoleSet;
    IQueryable<User> IAlmerData.Users => UserSet;
    IQueryable<Product> IAlmerData.Products => ProductSet;
    IQueryable<StockMovement> IAlmerData.StockMovements => StockMovementSet;
    IQueryable<TenantSetting> IAlmerData.TenantSettings => TenantSettingSet;
    void IAlmerData.Add<T>(T entity) => Add(entity);

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Tenant>().HasIndex(x => x.Slug).IsUnique();
        b.Entity<LegalEntity>().HasAlternateKey(x => new { x.TenantId, x.Id });
        b.Entity<Branch>().HasAlternateKey(x => new { x.TenantId, x.Id });
        b.Entity<Role>().HasAlternateKey(x => new { x.TenantId, x.Id });
        b.Entity<ProductCategory>().HasAlternateKey(x => new { x.TenantId, x.Id });
        b.Entity<UnitOfMeasure>().HasAlternateKey(x => new { x.TenantId, x.Id });
        b.Entity<Product>().HasAlternateKey(x => new { x.TenantId, x.Id });
        b.Entity<Supplier>().HasAlternateKey(x => new { x.TenantId, x.Id });
        b.Entity<Purchase>().HasAlternateKey(x => new { x.TenantId, x.Id });
        b.Entity<Sale>().HasAlternateKey(x => new { x.TenantId, x.Id });
        b.Entity<FinancialCategory>().HasAlternateKey(x => new { x.TenantId, x.Id });
        b.Entity<ImportBatch>().HasAlternateKey(x => new { x.TenantId, x.Id });
        b.Entity<LegalEntity>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Branch>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Branch>().HasOne<LegalEntity>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Role>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<LegalEntity>().OwnsOne(x => x.Name);
        b.Entity<Branch>().OwnsOne(x => x.Name);
        b.Entity<Role>().OwnsOne(x => x.Name);
        b.Entity<ProductCategory>().OwnsOne(x => x.Name);
        b.Entity<UnitOfMeasure>().OwnsOne(x => x.Name);
        b.Entity<Product>().OwnsOne(x => x.Name);
        b.Entity<Product>().OwnsOne(x => x.Description);
        b.Entity<TaxRule>().OwnsOne(x => x.Name);
        b.Entity<FinancialCategory>().OwnsOne(x => x.Name);
        b.Entity<Campaign>().OwnsOne(x => x.Name);
        b.Entity<UserTenant>().HasKey(x => new { x.UserId, x.TenantId });
        b.Entity<UserBranch>().HasKey(x => new { x.UserId, x.TenantId, x.BranchId });
        b.Entity<UserSession>().HasIndex(x => x.RefreshTokenHash).IsUnique();
        b.Entity<UserSession>().HasIndex(x => new { x.TenantId, x.UserId, x.RevokedAt, x.ExpiresAt });
        b.Entity<UserTenant>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<UserTenant>().HasOne<Role>().WithMany().HasForeignKey(x => new { x.TenantId, x.RoleId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<UserBranch>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<UserBranch>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<UserBranch>().HasOne<Branch>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        b.Entity<UserSession>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<UserSession>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<UserSession>().HasOne<UserSession>().WithMany().HasForeignKey(x => x.ReplacedBySessionId).OnDelete(DeleteBehavior.SetNull).IsRequired(false);
        b.Entity<RolePermission>().HasOne<Role>().WithMany(x => x.Permissions).HasForeignKey(x => new { x.TenantId, x.RoleId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        b.Entity<DailyMetric>().HasOne<Branch>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AuditEntry>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ProductCategory>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ProductCategory>().HasOne<ProductCategory>().WithMany().HasForeignKey(x => new { x.TenantId, x.ParentId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        b.Entity<UnitOfMeasure>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<UnitOfMeasure>().HasOne<UnitOfMeasure>().WithMany().HasForeignKey(x => new { x.TenantId, x.BaseUnitId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        b.Entity<Product>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Product>().HasOne<ProductCategory>().WithMany().HasForeignKey(x => new { x.TenantId, x.CategoryId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Product>().HasOne<UnitOfMeasure>().WithMany().HasForeignKey(x => new { x.TenantId, x.UnitId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ProductPrice>().HasOne<Product>().WithMany().HasForeignKey(x => new { x.TenantId, x.ProductId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ProductPrice>().HasOne<Branch>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        b.Entity<StockMovement>().HasOne<Product>().WithMany().HasForeignKey(x => new { x.TenantId, x.ProductId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<StockMovement>().HasOne<Branch>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<StockMovement>().HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<TaxRule>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Supplier>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Purchase>().HasOne<Branch>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Purchase>().HasOne<Supplier>().WithMany().HasForeignKey(x => new { x.TenantId, x.SupplierId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Purchase>().HasMany(x => x.Lines).WithOne().HasForeignKey(x => new { x.TenantId, x.PurchaseId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PurchaseLine>().HasOne<Product>().WithMany().HasForeignKey(x => new { x.TenantId, x.ProductId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Sale>().HasOne<Branch>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Sale>().HasMany(x => x.Lines).WithOne().HasForeignKey(x => new { x.TenantId, x.SaleId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<SaleLine>().HasOne<Product>().WithMany().HasForeignKey(x => new { x.TenantId, x.ProductId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinancialCategory>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinancialEntry>().HasOne<FinancialCategory>().WithMany().HasForeignKey(x => new { x.TenantId, x.CategoryId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinancialEntry>().HasOne<Branch>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        b.Entity<Campaign>().HasOne<Product>().WithMany().HasForeignKey(x => new { x.TenantId, x.ProductId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        b.Entity<Campaign>().HasOne<ProductCategory>().WithMany().HasForeignKey(x => new { x.TenantId, x.CategoryId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        b.Entity<Campaign>().HasOne<Branch>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        b.Entity<ImportBatch>().HasOne<Branch>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        b.Entity<ImportBatch>().HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ImportedRecordKey>().HasOne<ImportBatch>().WithMany().HasForeignKey(x => new { x.TenantId, x.ImportBatchId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<TenantSetting>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<DashboardPreference>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<DashboardPreference>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Notification>().OwnsOne(x => x.Title);
        b.Entity<Notification>().OwnsOne(x => x.Message);
        b.Entity<Notification>().HasAlternateKey(x => new { x.TenantId, x.Id });
        b.Entity<Notification>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Notification>().HasOne<Branch>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Cascade).IsRequired(false);
        b.Entity<Notification>().HasOne<Product>().WithMany().HasForeignKey(x => new { x.TenantId, x.ProductId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Cascade).IsRequired(false);
        b.Entity<NotificationRead>().HasKey(x => new { x.TenantId, x.NotificationId, x.UserId });
        b.Entity<NotificationRead>().HasOne<Notification>().WithMany().HasForeignKey(x => new { x.TenantId, x.NotificationId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        b.Entity<NotificationRead>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<NotificationChannelSetting>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<NotificationChannelSetting>().HasIndex(x => x.TenantId).IsUnique();
        b.Entity<BackgroundJob>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<BackgroundJob>().HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        b.Entity<BackgroundJob>().HasIndex(x => new { x.Status, x.AvailableAt, x.CreatedAt });
        b.Entity<RolePermission>().HasKey(x => new { x.TenantId, x.RoleId, x.PermissionCode });
        b.Entity<User>().HasIndex(x => x.Email).IsUnique();
        b.Entity<AccountToken>().HasIndex(x => x.TokenHash).IsUnique();
        b.Entity<AccountToken>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Branch>().HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        b.Entity<DailyMetric>().HasIndex(x => new { x.TenantId, x.BranchId, x.Date });
        b.Entity<DailyMetric>().Property(x => x.GrossSales).HasPrecision(18, 2);
        b.Entity<DailyMetric>().Property(x => x.Vat).HasPrecision(18, 2);
        b.Entity<DailyMetric>().Property(x => x.Returns).HasPrecision(18, 2);
        b.Entity<DailyMetric>().Property(x => x.Discounts).HasPrecision(18, 2);
        b.Entity<DailyMetric>().Property(x => x.CostOfGoodsSold).HasPrecision(18, 2);
        b.Entity<DailyMetric>().Property(x => x.OperatingExpenses).HasPrecision(18, 2);
        b.Entity<UnitOfMeasure>().Property(x => x.ConversionFactor).HasPrecision(18, 6);
        b.Entity<Product>().Property(x => x.PurchaseCost).HasPrecision(18, 2);
        b.Entity<Product>().Property(x => x.SalesPrice).HasPrecision(18, 2);
        b.Entity<Product>().Property(x => x.MinimumStock).HasPrecision(18, 3);
        b.Entity<ProductPrice>().Property(x => x.Price).HasPrecision(18, 2);
        b.Entity<StockMovement>().Property(x => x.QuantityDelta).HasPrecision(18, 3);
        b.Entity<StockMovement>().Property(x => x.UnitCost).HasPrecision(18, 2);
        b.Entity<Product>().HasIndex(x => new { x.TenantId, x.Sku }).IsUnique();
        b.Entity<Product>().HasIndex(x => new { x.TenantId, x.Barcode }).IsUnique();
        b.Entity<StockMovement>().HasIndex(x => new { x.TenantId, x.BranchId, x.ProductId, x.OccurredAt });
        b.Entity<ProductPrice>().HasIndex(x => new { x.TenantId, x.ProductId, x.BranchId, x.ValidFrom });
        b.Entity<TaxRule>().HasIndex(x => new { x.TenantId, x.Code, x.ValidFrom }).IsUnique();
        b.Entity<Supplier>().HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        b.Entity<Purchase>().HasIndex(x => new { x.TenantId, x.DocumentNumber }).IsUnique();
        b.Entity<Sale>().HasIndex(x => new { x.TenantId, x.TransactionNumber }).IsUnique();
        b.Entity<ImportBatch>().HasIndex(x => new { x.TenantId, x.Type, x.BranchId, x.FileHash }).IsUnique();
        b.Entity<ImportMappingTemplate>().HasIndex(x => new { x.TenantId, x.Type, x.Name }).IsUnique();
        b.Entity<ImportedRecordKey>().HasIndex(x => new { x.TenantId, x.Type, x.ExternalKey }).IsUnique();
        b.Entity<TenantSetting>().HasIndex(x => x.TenantId).IsUnique();
        b.Entity<DashboardPreference>().HasIndex(x => new { x.TenantId, x.UserId }).IsUnique();
        b.Entity<Notification>().HasIndex(x => new { x.TenantId, x.ExternalKey }).IsUnique();
        b.Entity<Notification>().HasIndex(x => new { x.TenantId, x.ResolvedAt, x.UpdatedAt });
        foreach (var property in new[] { b.Entity<Purchase>().Property(x => x.NetAmount), b.Entity<Purchase>().Property(x => x.TaxAmount), b.Entity<Purchase>().Property(x => x.GrossAmount) }) property.HasPrecision(18, 2);
        foreach (var property in new[] { b.Entity<Sale>().Property(x => x.NetAmount), b.Entity<Sale>().Property(x => x.TaxAmount), b.Entity<Sale>().Property(x => x.GrossAmount), b.Entity<Sale>().Property(x => x.DiscountAmount) }) property.HasPrecision(18, 2);
        b.Entity<TaxRule>().Property(x => x.Rate).HasPrecision(8, 4);
        b.Entity<Campaign>().Property(x => x.Value).HasPrecision(18, 2);
        b.Entity<LegalEntity>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<Branch>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<UserTenant>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<UserBranch>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<UserSession>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<Role>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<RolePermission>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<DailyMetric>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<AuditEntry>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<ProductCategory>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<UnitOfMeasure>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<Product>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<ProductPrice>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<StockMovement>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<TaxRule>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<Supplier>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<Purchase>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<PurchaseLine>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<Sale>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<SaleLine>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<FinancialCategory>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<FinancialEntry>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<Campaign>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<ImportBatch>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<ImportMappingTemplate>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<ImportedRecordKey>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<TenantSetting>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<DashboardPreference>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<Notification>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<NotificationRead>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<NotificationChannelSetting>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        b.Entity<BackgroundJob>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var changed = ChangeTracker.Entries<ITenantEntity>().Where(x => x.Entity is not AuditEntry && x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray();
        if (changed.Any(x => x.Entity is StockMovement && x.State is EntityState.Modified or EntityState.Deleted)) throw new InvalidOperationException("Stock movements are immutable; create a correcting movement instead.");
        if (changed.Any(x => x.Entity is Sale or SaleLine or Purchase or PurchaseLine && x.State is EntityState.Modified or EntityState.Deleted)) throw new InvalidOperationException("Posted sales and purchases are immutable; create a return or correction instead.");
        foreach (var entry in changed)
        {
            if (CurrentTenantId == Guid.Empty && !AllowDevelopmentSeed) throw new UnauthorizedAccessException("A tenant context is required for writes.");
            if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty) entry.Entity.TenantId = CurrentTenantId;
            if (!AllowDevelopmentSeed && entry.Entity.TenantId != CurrentTenantId) throw new UnauthorizedAccessException("Cross-tenant write rejected.");
        }
        if (!AllowDevelopmentSeed && CurrentTenantId != Guid.Empty)
        {
            foreach (var entry in changed)
            {
                var id = entry.Properties.FirstOrDefault(x => x.Metadata.Name == "Id")?.CurrentValue?.ToString()
                    ?? string.Join(':', entry.Properties.Where(x => x.Metadata.IsPrimaryKey()).Select(x => x.CurrentValue));
                AuditEntrySet.Add(new AuditEntry { Id = Guid.NewGuid(), TenantId = CurrentTenantId, UserId = tenantContext.UserId == Guid.Empty ? null : tenantContext.UserId, Action = entry.State.ToString(), EntityType = entry.Metadata.ClrType.Name, EntityId = id, OccurredAt = DateTimeOffset.UtcNow });
            }
        }
        return await base.SaveChangesAsync(cancellationToken);
    }
}

