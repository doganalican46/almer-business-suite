CREATE TABLE IF NOT EXISTS "ProductCategorySet" (
  "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL REFERENCES "TenantSet"("Id") ON DELETE RESTRICT,
  "ParentId" uuid, "Name_De" text NOT NULL, "Name_Tr" text NOT NULL,
  "SortOrder" integer NOT NULL DEFAULT 0, "IsActive" boolean NOT NULL DEFAULT true,
  CONSTRAINT "AK_ProductCategorySet_TenantId_Id" UNIQUE ("TenantId", "Id"),
  CONSTRAINT "FK_ProductCategory_Parent" FOREIGN KEY ("TenantId", "ParentId") REFERENCES "ProductCategorySet"("TenantId", "Id") ON DELETE RESTRICT
);
CREATE TABLE IF NOT EXISTS "UnitOfMeasureSet" (
  "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL REFERENCES "TenantSet"("Id") ON DELETE RESTRICT,
  "Name_De" text NOT NULL, "Name_Tr" text NOT NULL, "Symbol" text NOT NULL,
  "BaseUnitId" uuid, "ConversionFactor" numeric(18,6) NOT NULL DEFAULT 1,
  "IsActive" boolean NOT NULL DEFAULT true,
  CONSTRAINT "AK_UnitOfMeasureSet_TenantId_Id" UNIQUE ("TenantId", "Id"),
  CONSTRAINT "FK_Unit_Base" FOREIGN KEY ("TenantId", "BaseUnitId") REFERENCES "UnitOfMeasureSet"("TenantId", "Id") ON DELETE RESTRICT
);
CREATE TABLE IF NOT EXISTS "ProductSet" (
  "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL REFERENCES "TenantSet"("Id") ON DELETE RESTRICT,
  "Sku" text NOT NULL, "Barcode" text, "Name_De" text NOT NULL, "Name_Tr" text NOT NULL,
  "Description_De" text NOT NULL DEFAULT '', "Description_Tr" text NOT NULL DEFAULT '', "Brand" text,
  "CategoryId" uuid NOT NULL, "UnitId" uuid NOT NULL, "TaxClassCode" text NOT NULL DEFAULT 'DE-7',
  "PurchaseCost" numeric(18,2) NOT NULL, "SalesPrice" numeric(18,2) NOT NULL,
  "MinimumStock" numeric(18,3) NOT NULL DEFAULT 0, "ImageUrl" text, "IsActive" boolean NOT NULL DEFAULT true,
  CONSTRAINT "AK_ProductSet_TenantId_Id" UNIQUE ("TenantId", "Id"),
  CONSTRAINT "FK_Product_Category" FOREIGN KEY ("TenantId", "CategoryId") REFERENCES "ProductCategorySet"("TenantId", "Id") ON DELETE RESTRICT,
  CONSTRAINT "FK_Product_Unit" FOREIGN KEY ("TenantId", "UnitId") REFERENCES "UnitOfMeasureSet"("TenantId", "Id") ON DELETE RESTRICT,
  CONSTRAINT "UQ_Product_Sku" UNIQUE ("TenantId", "Sku"), CONSTRAINT "UQ_Product_Barcode" UNIQUE ("TenantId", "Barcode")
);
CREATE TABLE IF NOT EXISTS "ProductPriceSet" (
  "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL, "ProductId" uuid NOT NULL, "BranchId" uuid,
  "Price" numeric(18,2) NOT NULL, "ValidFrom" timestamptz NOT NULL, "ValidTo" timestamptz,
  CONSTRAINT "FK_Price_Product" FOREIGN KEY ("TenantId", "ProductId") REFERENCES "ProductSet"("TenantId", "Id") ON DELETE RESTRICT,
  CONSTRAINT "FK_Price_Branch" FOREIGN KEY ("TenantId", "BranchId") REFERENCES "BranchSet"("TenantId", "Id") ON DELETE RESTRICT
);
CREATE INDEX IF NOT EXISTS "IX_Price_Product_Branch_From" ON "ProductPriceSet" ("TenantId", "ProductId", "BranchId", "ValidFrom");
CREATE TABLE IF NOT EXISTS "StockMovementSet" (
  "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL, "BranchId" uuid NOT NULL, "ProductId" uuid NOT NULL,
  "Type" integer NOT NULL, "QuantityDelta" numeric(18,3) NOT NULL, "UnitCost" numeric(18,2),
  "Reference" text, "Note" text, "LotNumber" text, "ExpiresOn" date, "TransferGroupId" uuid,
  "CreatedByUserId" uuid NOT NULL, "OccurredAt" timestamptz NOT NULL,
  CONSTRAINT "FK_Stock_Product" FOREIGN KEY ("TenantId", "ProductId") REFERENCES "ProductSet"("TenantId", "Id") ON DELETE RESTRICT,
  CONSTRAINT "FK_Stock_Branch" FOREIGN KEY ("TenantId", "BranchId") REFERENCES "BranchSet"("TenantId", "Id") ON DELETE RESTRICT,
  CONSTRAINT "FK_Stock_User" FOREIGN KEY ("CreatedByUserId") REFERENCES "UserSet"("Id") ON DELETE RESTRICT
);
CREATE INDEX IF NOT EXISTS "IX_Stock_Tenant_Branch_Product_Time" ON "StockMovementSet" ("TenantId", "BranchId", "ProductId", "OccurredAt");
