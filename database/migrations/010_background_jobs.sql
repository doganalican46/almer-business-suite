CREATE TABLE IF NOT EXISTS "BackgroundJobSet" (
  "Id" uuid PRIMARY KEY,
  "TenantId" uuid NOT NULL REFERENCES "TenantSet"("Id") ON DELETE CASCADE,
  "Type" integer NOT NULL,
  "Status" integer NOT NULL,
  "PayloadJson" text NOT NULL DEFAULT '{}',
  "BranchIdsJson" text NOT NULL DEFAULT '[]',
  "Progress" integer NOT NULL DEFAULT 0,
  "Attempts" integer NOT NULL DEFAULT 0,
  "MaxAttempts" integer NOT NULL DEFAULT 3,
  "Error" text,
  "CreatedByUserId" uuid REFERENCES "UserSet"("Id") ON DELETE RESTRICT,
  "CreatedAt" timestamptz NOT NULL,
  "AvailableAt" timestamptz NOT NULL,
  "LockedAt" timestamptz,
  "CompletedAt" timestamptz
);
CREATE INDEX IF NOT EXISTS "IX_BackgroundJob_Claim" ON "BackgroundJobSet" ("Status", "AvailableAt", "CreatedAt");
ALTER TABLE "ImportBatchSet" ADD COLUMN IF NOT EXISTS "BackgroundJobId" uuid REFERENCES "BackgroundJobSet"("Id") ON DELETE SET NULL;
ALTER TABLE "ImportBatchSet" ADD COLUMN IF NOT EXISTS "FailureMessage" text;
CREATE UNIQUE INDEX IF NOT EXISTS "UX_ImportBatch_ActiveJob" ON "BackgroundJobSet" ((("PayloadJson"::jsonb)->>'importBatchId')) WHERE "Type" = 0 AND "Status" IN (0, 1);
CREATE UNIQUE INDEX IF NOT EXISTS "UX_NotificationRefresh_ActiveJob" ON "BackgroundJobSet" ("TenantId", "Type") WHERE "Type" = 1 AND "Status" IN (0, 1);
