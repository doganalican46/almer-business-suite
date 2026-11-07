CREATE TABLE IF NOT EXISTS "ImportBatchSet" (
  "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL REFERENCES "TenantSet"("Id") ON DELETE RESTRICT,
  "BranchId" uuid, "Type" integer NOT NULL, "Status" integer NOT NULL,
  "FileName" text NOT NULL, "ContentType" text NOT NULL, "FileHash" text NOT NULL, "SourceData" bytea NOT NULL,
  "MappingJson" text NOT NULL, "OptionsJson" text NOT NULL, "ErrorsJson" text NOT NULL,
  "TotalRows" integer NOT NULL, "ValidRows" integer NOT NULL, "ErrorRows" integer NOT NULL, "ImportedRows" integer NOT NULL,
  "CreatedByUserId" uuid NOT NULL REFERENCES "UserSet"("Id") ON DELETE RESTRICT,
  "CreatedAt" timestamptz NOT NULL, "CompletedAt" timestamptz,
  CONSTRAINT "AK_ImportBatchSet_TenantId_Id" UNIQUE ("TenantId", "Id"),
  FOREIGN KEY ("TenantId", "BranchId") REFERENCES "BranchSet"("TenantId", "Id") ON DELETE RESTRICT,
  UNIQUE ("TenantId", "Type", "BranchId", "FileHash")
);
CREATE TABLE IF NOT EXISTS "ImportMappingTemplateSet" (
  "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL REFERENCES "TenantSet"("Id") ON DELETE RESTRICT,
  "Name" text NOT NULL, "Type" integer NOT NULL, "MappingJson" text NOT NULL, "OptionsJson" text NOT NULL,
  "CreatedAt" timestamptz NOT NULL, UNIQUE ("TenantId", "Type", "Name")
);
CREATE TABLE IF NOT EXISTS "ImportedRecordKeySet" (
  "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL REFERENCES "TenantSet"("Id") ON DELETE RESTRICT,
  "Type" integer NOT NULL, "ExternalKey" text NOT NULL, "ImportBatchId" uuid NOT NULL,
  FOREIGN KEY ("TenantId", "ImportBatchId") REFERENCES "ImportBatchSet"("TenantId", "Id") ON DELETE RESTRICT,
  UNIQUE ("TenantId", "Type", "ExternalKey")
);
CREATE INDEX IF NOT EXISTS "IX_ImportBatch_CreatedAt" ON "ImportBatchSet" ("TenantId", "CreatedAt" DESC);
