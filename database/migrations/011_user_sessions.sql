CREATE TABLE IF NOT EXISTS "UserSessionSet" (
  "Id" uuid PRIMARY KEY,
  "TenantId" uuid NOT NULL REFERENCES "TenantSet"("Id") ON DELETE CASCADE,
  "UserId" uuid NOT NULL REFERENCES "UserSet"("Id") ON DELETE CASCADE,
  "FamilyId" uuid NOT NULL,
  "RefreshTokenHash" text NOT NULL UNIQUE,
  "UserAgent" text,
  "IpAddress" text,
  "CreatedAt" timestamptz NOT NULL,
  "LastUsedAt" timestamptz NOT NULL,
  "ExpiresAt" timestamptz NOT NULL,
  "RevokedAt" timestamptz,
  "RevokeReason" text,
  "ReplacedBySessionId" uuid REFERENCES "UserSessionSet"("Id") ON DELETE SET NULL
);
CREATE INDEX IF NOT EXISTS "IX_UserSession_Active" ON "UserSessionSet" ("TenantId", "UserId", "RevokedAt", "ExpiresAt");
