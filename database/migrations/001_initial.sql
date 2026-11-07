-- Almer Business Suite / Phase 1-2 baseline
-- EF uses quoted PascalCase identifiers; all transactional tables carry TenantId.
CREATE TABLE IF NOT EXISTS "TenantSet" (
  "Id" uuid PRIMARY KEY, "Name" text NOT NULL, "Slug" text NOT NULL UNIQUE,
  "DefaultLocale" text NOT NULL DEFAULT 'de-DE', "Currency" text NOT NULL DEFAULT 'EUR',
  "IsDemo" boolean NOT NULL DEFAULT false, "IsActive" boolean NOT NULL DEFAULT true
);
CREATE TABLE IF NOT EXISTS "LegalEntitySet" (
  "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL REFERENCES "TenantSet"("Id") ON DELETE RESTRICT,
  "Name_De" text NOT NULL, "Name_Tr" text NOT NULL, "TradeName" text NOT NULL,
  "TaxNumber" text NOT NULL, "CountryCode" text NOT NULL, "IsActive" boolean NOT NULL DEFAULT true
);
CREATE TABLE IF NOT EXISTS "BranchSet" (
  "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL REFERENCES "TenantSet"("Id") ON DELETE RESTRICT,
  "LegalEntityId" uuid NOT NULL REFERENCES "LegalEntitySet"("Id") ON DELETE RESTRICT,
  "Name_De" text NOT NULL, "Name_Tr" text NOT NULL, "Code" text NOT NULL, "Address" text NOT NULL,
  "ManagerName" text, "Phone" text, "OpenedOn" date, "IsActive" boolean NOT NULL DEFAULT true,
  UNIQUE ("TenantId", "Code")
);
CREATE TABLE IF NOT EXISTS "UserSet" (
  "Id" uuid PRIMARY KEY, "Email" text NOT NULL UNIQUE, "DisplayName" text NOT NULL,
  "PasswordHash" text NOT NULL, "PreferredLocale" text NOT NULL DEFAULT 'de-DE', "PreferredTheme" text NOT NULL DEFAULT 'light'
);
CREATE TABLE IF NOT EXISTS "RoleSet" (
  "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL REFERENCES "TenantSet"("Id") ON DELETE RESTRICT,
  "Name_De" text NOT NULL, "Name_Tr" text NOT NULL, "IsSystem" boolean NOT NULL DEFAULT false
);
CREATE TABLE IF NOT EXISTS "UserTenantSet" (
  "UserId" uuid NOT NULL REFERENCES "UserSet"("Id") ON DELETE CASCADE,
  "TenantId" uuid NOT NULL REFERENCES "TenantSet"("Id") ON DELETE CASCADE,
  "RoleId" uuid NOT NULL REFERENCES "RoleSet"("Id") ON DELETE RESTRICT,
  PRIMARY KEY ("UserId", "TenantId")
);
CREATE TABLE IF NOT EXISTS "RolePermissionSet" (
  "TenantId" uuid NOT NULL REFERENCES "TenantSet"("Id") ON DELETE CASCADE,
  "RoleId" uuid NOT NULL REFERENCES "RoleSet"("Id") ON DELETE CASCADE,
  "PermissionCode" text NOT NULL, PRIMARY KEY ("TenantId", "RoleId", "PermissionCode")
);
CREATE TABLE IF NOT EXISTS "DailyMetricSet" (
  "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL REFERENCES "TenantSet"("Id") ON DELETE RESTRICT,
  "BranchId" uuid NOT NULL REFERENCES "BranchSet"("Id") ON DELETE RESTRICT, "Date" date NOT NULL,
  "GrossSales" numeric(18,2) NOT NULL, "Vat" numeric(18,2) NOT NULL, "Returns" numeric(18,2) NOT NULL,
  "Discounts" numeric(18,2) NOT NULL, "CostOfGoodsSold" numeric(18,2) NOT NULL,
  "OperatingExpenses" numeric(18,2) NOT NULL, "TransactionCount" integer NOT NULL,
  "SalesLines" integer NOT NULL, "CostMatchedLines" integer NOT NULL
);
CREATE INDEX IF NOT EXISTS "IX_DailyMetric_Tenant_Branch_Date" ON "DailyMetricSet" ("TenantId", "BranchId", "Date");
CREATE TABLE IF NOT EXISTS "AuditEntrySet" (
  "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL REFERENCES "TenantSet"("Id") ON DELETE RESTRICT,
  "UserId" uuid, "Action" text NOT NULL, "EntityType" text NOT NULL, "EntityId" text NOT NULL,
  "OccurredAt" timestamptz NOT NULL
);
