ALTER TABLE "UserSet" ADD COLUMN IF NOT EXISTS "MfaEnabled" boolean NOT NULL DEFAULT false;
ALTER TABLE "UserSet" ADD COLUMN IF NOT EXISTS "MfaSecretProtected" text;
ALTER TABLE "UserSet" ADD COLUMN IF NOT EXISTS "MfaPendingSecretProtected" text;
ALTER TABLE "UserSet" ADD COLUMN IF NOT EXISTS "RecoveryCodesJson" text NOT NULL DEFAULT '[]';
