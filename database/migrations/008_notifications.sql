CREATE TABLE IF NOT EXISTS "NotificationSet" (
  "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL REFERENCES "TenantSet"("Id") ON DELETE CASCADE,
  "BranchId" uuid, "ProductId" uuid, "ExternalKey" text NOT NULL, "Type" integer NOT NULL, "Severity" integer NOT NULL,
  "Title_De" text NOT NULL, "Title_Tr" text NOT NULL, "Message_De" text NOT NULL, "Message_Tr" text NOT NULL,
  "TargetUrl" text, "CreatedAt" timestamptz NOT NULL, "UpdatedAt" timestamptz NOT NULL, "ResolvedAt" timestamptz,
  CONSTRAINT "AK_NotificationSet_TenantId_Id" UNIQUE ("TenantId", "Id"),
  FOREIGN KEY ("TenantId", "BranchId") REFERENCES "BranchSet"("TenantId", "Id") ON DELETE CASCADE,
  FOREIGN KEY ("TenantId", "ProductId") REFERENCES "ProductSet"("TenantId", "Id") ON DELETE CASCADE,
  UNIQUE ("TenantId", "ExternalKey")
);
CREATE TABLE IF NOT EXISTS "NotificationReadSet" (
  "TenantId" uuid NOT NULL, "NotificationId" uuid NOT NULL, "UserId" uuid NOT NULL REFERENCES "UserSet"("Id") ON DELETE CASCADE,
  "ReadAt" timestamptz NOT NULL, PRIMARY KEY ("TenantId", "NotificationId", "UserId"),
  FOREIGN KEY ("TenantId", "NotificationId") REFERENCES "NotificationSet"("TenantId", "Id") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS "IX_Notification_Active" ON "NotificationSet" ("TenantId", "ResolvedAt", "UpdatedAt" DESC);
