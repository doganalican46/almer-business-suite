CREATE TABLE IF NOT EXISTS "TaxRuleSet" (
  "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL REFERENCES "TenantSet"("Id") ON DELETE RESTRICT,
  "Code" text NOT NULL, "CountryCode" text NOT NULL, "Name_De" text NOT NULL, "Name_Tr" text NOT NULL,
  "Rate" numeric(8,4) NOT NULL, "ValidFrom" date NOT NULL, "ValidTo" date, "IsActive" boolean NOT NULL DEFAULT true,
  UNIQUE ("TenantId", "Code", "ValidFrom")
);
CREATE TABLE IF NOT EXISTS "SupplierSet" (
  "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL REFERENCES "TenantSet"("Id") ON DELETE RESTRICT,
  "Code" text NOT NULL, "Name" text NOT NULL, "TaxNumber" text, "Email" text, "Phone" text, "Address" text,
  "IsActive" boolean NOT NULL DEFAULT true, CONSTRAINT "AK_SupplierSet_TenantId_Id" UNIQUE ("TenantId", "Id"), UNIQUE ("TenantId", "Code")
);
CREATE TABLE IF NOT EXISTS "PurchaseSet" (
  "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL, "BranchId" uuid NOT NULL, "SupplierId" uuid NOT NULL,
  "DocumentNumber" text NOT NULL, "Date" date NOT NULL, "NetAmount" numeric(18,2) NOT NULL,
  "TaxAmount" numeric(18,2) NOT NULL, "GrossAmount" numeric(18,2) NOT NULL,
  "CreatedByUserId" uuid NOT NULL REFERENCES "UserSet"("Id") ON DELETE RESTRICT, "CreatedAt" timestamptz NOT NULL,
  CONSTRAINT "AK_PurchaseSet_TenantId_Id" UNIQUE ("TenantId", "Id"), UNIQUE ("TenantId", "DocumentNumber"),
  FOREIGN KEY ("TenantId", "BranchId") REFERENCES "BranchSet"("TenantId", "Id") ON DELETE RESTRICT,
  FOREIGN KEY ("TenantId", "SupplierId") REFERENCES "SupplierSet"("TenantId", "Id") ON DELETE RESTRICT
);
CREATE TABLE IF NOT EXISTS "PurchaseLineSet" (
  "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL, "PurchaseId" uuid NOT NULL, "ProductId" uuid NOT NULL,
  "ProductNameSnapshot" text NOT NULL, "Quantity" numeric(18,3) NOT NULL, "UnitCost" numeric(18,2) NOT NULL,
  "TaxRate" numeric(8,4) NOT NULL, "NetAmount" numeric(18,2) NOT NULL, "TaxAmount" numeric(18,2) NOT NULL, "GrossAmount" numeric(18,2) NOT NULL,
  FOREIGN KEY ("TenantId", "PurchaseId") REFERENCES "PurchaseSet"("TenantId", "Id") ON DELETE RESTRICT,
  FOREIGN KEY ("TenantId", "ProductId") REFERENCES "ProductSet"("TenantId", "Id") ON DELETE RESTRICT
);
CREATE TABLE IF NOT EXISTS "SaleSet" (
  "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL, "BranchId" uuid NOT NULL, "TransactionNumber" text NOT NULL,
  "OccurredAt" timestamptz NOT NULL, "PaymentMethod" text NOT NULL, "SourceSystem" text NOT NULL, "IsReturn" boolean NOT NULL,
  "NetAmount" numeric(18,2) NOT NULL, "TaxAmount" numeric(18,2) NOT NULL, "GrossAmount" numeric(18,2) NOT NULL,
  "DiscountAmount" numeric(18,2) NOT NULL, "CreatedByUserId" uuid NOT NULL REFERENCES "UserSet"("Id") ON DELETE RESTRICT,
  CONSTRAINT "AK_SaleSet_TenantId_Id" UNIQUE ("TenantId", "Id"), UNIQUE ("TenantId", "TransactionNumber"),
  FOREIGN KEY ("TenantId", "BranchId") REFERENCES "BranchSet"("TenantId", "Id") ON DELETE RESTRICT
);
CREATE TABLE IF NOT EXISTS "SaleLineSet" (
  "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL, "SaleId" uuid NOT NULL, "ProductId" uuid NOT NULL,
  "ProductNameSnapshot" text NOT NULL, "Quantity" numeric(18,3) NOT NULL, "UnitPrice" numeric(18,2) NOT NULL,
  "UnitCost" numeric(18,2) NOT NULL, "DiscountAmount" numeric(18,2) NOT NULL, "TaxRate" numeric(8,4) NOT NULL,
  "NetAmount" numeric(18,2) NOT NULL, "TaxAmount" numeric(18,2) NOT NULL, "GrossAmount" numeric(18,2) NOT NULL,
  FOREIGN KEY ("TenantId", "SaleId") REFERENCES "SaleSet"("TenantId", "Id") ON DELETE RESTRICT,
  FOREIGN KEY ("TenantId", "ProductId") REFERENCES "ProductSet"("TenantId", "Id") ON DELETE RESTRICT
);
CREATE TABLE IF NOT EXISTS "FinancialCategorySet" (
  "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL REFERENCES "TenantSet"("Id") ON DELETE RESTRICT,
  "Type" integer NOT NULL, "Name_De" text NOT NULL, "Name_Tr" text NOT NULL, "IsActive" boolean NOT NULL DEFAULT true,
  CONSTRAINT "AK_FinancialCategorySet_TenantId_Id" UNIQUE ("TenantId", "Id")
);
CREATE TABLE IF NOT EXISTS "FinancialEntrySet" (
  "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL, "BranchId" uuid, "CategoryId" uuid NOT NULL, "Type" integer NOT NULL,
  "Date" date NOT NULL, "NetAmount" numeric(18,2) NOT NULL, "TaxRate" numeric(8,4) NOT NULL,
  "TaxAmount" numeric(18,2) NOT NULL, "GrossAmount" numeric(18,2) NOT NULL, "Description" text NOT NULL,
  "PaymentMethod" text NOT NULL, "IsRecurring" boolean NOT NULL, "IsApproved" boolean NOT NULL, "DocumentUrl" text,
  "CreatedByUserId" uuid NOT NULL REFERENCES "UserSet"("Id") ON DELETE RESTRICT, "CreatedAt" timestamptz NOT NULL,
  FOREIGN KEY ("TenantId", "CategoryId") REFERENCES "FinancialCategorySet"("TenantId", "Id") ON DELETE RESTRICT,
  FOREIGN KEY ("TenantId", "BranchId") REFERENCES "BranchSet"("TenantId", "Id") ON DELETE RESTRICT
);
CREATE TABLE IF NOT EXISTS "CampaignSet" (
  "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL REFERENCES "TenantSet"("Id") ON DELETE RESTRICT,
  "Name_De" text NOT NULL, "Name_Tr" text NOT NULL, "Type" integer NOT NULL, "Value" numeric(18,2) NOT NULL,
  "ProductId" uuid, "CategoryId" uuid, "BranchId" uuid, "StartsAt" timestamptz NOT NULL, "EndsAt" timestamptz NOT NULL,
  "Priority" integer NOT NULL, "IsActive" boolean NOT NULL DEFAULT true,
  FOREIGN KEY ("TenantId", "ProductId") REFERENCES "ProductSet"("TenantId", "Id") ON DELETE RESTRICT,
  FOREIGN KEY ("TenantId", "CategoryId") REFERENCES "ProductCategorySet"("TenantId", "Id") ON DELETE RESTRICT,
  FOREIGN KEY ("TenantId", "BranchId") REFERENCES "BranchSet"("TenantId", "Id") ON DELETE RESTRICT
);
CREATE INDEX IF NOT EXISTS "IX_Sale_Date_Branch" ON "SaleSet" ("TenantId", "OccurredAt", "BranchId");
CREATE INDEX IF NOT EXISTS "IX_Financial_Date_Branch" ON "FinancialEntrySet" ("TenantId", "Date", "BranchId");
