CREATE TABLE IF NOT EXISTS "NotificationChannelSettingSet" (
  "Id" uuid PRIMARY KEY,
  "TenantId" uuid NOT NULL UNIQUE REFERENCES "TenantSet"("Id") ON DELETE CASCADE,
  "EmailEnabled" boolean NOT NULL DEFAULT false,
  "EmailRecipientsJson" text NOT NULL DEFAULT '[]',
  "WebhookEnabled" boolean NOT NULL DEFAULT false,
  "WebhookUrl" text,
  "WebhookSecretProtected" text
);
CREATE UNIQUE INDEX IF NOT EXISTS "UX_NotificationDelivery_Event" ON "BackgroundJobSet" ("TenantId", "Type", (("PayloadJson"::jsonb)->>'eventId')) WHERE "Type" IN (2, 3);
