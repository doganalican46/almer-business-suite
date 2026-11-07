# Faz 9 — Hesap güvenliği ve MFA

Implemented account-wide two-factor authentication without changing tenant or branch authorization semantics.

- RFC 6238 compatible, six-digit TOTP with a one-step clock-skew window.
- Authenticator setup via manual Base32 key and standard `otpauth://` URI.
- MFA secret encrypted at rest through ASP.NET Core Data Protection.
- Eight high-entropy, one-time recovery codes; only SHA-256 hashes are stored.
- Login returns HTTP 428 while a second factor is required and issues no JWT before verification.
- Every authenticated user can manage MFA from the account security screen.
- Data Protection keys are persisted in a dedicated Docker volume.
- German and Turkish UI copy included.

Operational note: a production deployment must back up the Data Protection key ring together with the database. Losing that key ring makes existing encrypted MFA secrets unreadable.
