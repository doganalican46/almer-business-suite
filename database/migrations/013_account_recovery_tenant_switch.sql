ALTER TABLE "UserSet" ADD COLUMN IF NOT EXISTS "EmailVerifiedAt" timestamptz;
CREATE TABLE IF NOT EXISTS "AccountTokenSet" (
  "Id" uuid PRIMARY KEY,
  "UserId" uuid NOT NULL REFERENCES "UserSet"("Id") ON DELETE CASCADE,
  "Type" integer NOT NULL,
  "TokenHash" text NOT NULL UNIQUE,
  "CreatedAt" timestamptz NOT NULL,
  "ExpiresAt" timestamptz NOT NULL,
  "ConsumedAt" timestamptz
);
CREATE INDEX IF NOT EXISTS "IX_AccountToken_User" ON "AccountTokenSet" ("UserId", "Type", "ExpiresAt");
