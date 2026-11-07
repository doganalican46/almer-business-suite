CREATE INDEX IF NOT EXISTS "IX_Purchase_Date_Branch" ON "PurchaseSet" ("TenantId", "Date", "BranchId");
CREATE INDEX IF NOT EXISTS "IX_SaleLine_Product" ON "SaleLineSet" ("TenantId", "ProductId", "SaleId");
CREATE INDEX IF NOT EXISTS "IX_PurchaseLine_Product" ON "PurchaseLineSet" ("TenantId", "ProductId", "PurchaseId");
CREATE INDEX IF NOT EXISTS "IX_Financial_Reporting" ON "FinancialEntrySet" ("TenantId", "Date", "Type", "IsApproved", "CategoryId");
