CREATE TABLE IF NOT EXISTS "TenantSettingSet" (
  "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL REFERENCES "TenantSet"("Id") ON DELETE CASCADE,
  "LogoUrl" text, "PrimaryColor" text NOT NULL DEFAULT '#147A46', "EnabledModulesJson" text NOT NULL DEFAULT '[]',
  "CriticalStockThreshold" integer NOT NULL DEFAULT 10, "ExpiryWarningDays" integer NOT NULL DEFAULT 14,
  UNIQUE ("TenantId")
);
CREATE TABLE IF NOT EXISTS "DashboardPreferenceSet" (
  "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL REFERENCES "TenantSet"("Id") ON DELETE CASCADE,
  "UserId" uuid NOT NULL REFERENCES "UserSet"("Id") ON DELETE CASCADE,
  "HiddenWidgetsJson" text NOT NULL DEFAULT '[]', "WidgetOrderJson" text NOT NULL DEFAULT '[]',
  UNIQUE ("TenantId", "UserId")
);
