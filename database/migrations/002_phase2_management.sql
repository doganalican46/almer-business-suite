ALTER TABLE "UserSet" ADD COLUMN IF NOT EXISTS "IsActive" boolean NOT NULL DEFAULT true;
ALTER TABLE "UserTenantSet" ADD COLUMN IF NOT EXISTS "IsActive" boolean NOT NULL DEFAULT true;
CREATE TABLE IF NOT EXISTS "UserBranchSet" (
  "UserId" uuid NOT NULL REFERENCES "UserSet"("Id") ON DELETE CASCADE,
  "TenantId" uuid NOT NULL REFERENCES "TenantSet"("Id") ON DELETE CASCADE,
  "BranchId" uuid NOT NULL REFERENCES "BranchSet"("Id") ON DELETE CASCADE,
  PRIMARY KEY ("UserId", "TenantId", "BranchId")
);
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'AK_LegalEntitySet_TenantId_Id') THEN ALTER TABLE "LegalEntitySet" ADD CONSTRAINT "AK_LegalEntitySet_TenantId_Id" UNIQUE ("TenantId", "Id"); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'AK_BranchSet_TenantId_Id') THEN ALTER TABLE "BranchSet" ADD CONSTRAINT "AK_BranchSet_TenantId_Id" UNIQUE ("TenantId", "Id"); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'AK_RoleSet_TenantId_Id') THEN ALTER TABLE "RoleSet" ADD CONSTRAINT "AK_RoleSet_TenantId_Id" UNIQUE ("TenantId", "Id"); END IF; END $$;
