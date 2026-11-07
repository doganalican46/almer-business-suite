# ADR 0007: Account-wide TOTP MFA

## Decision

MFA belongs to the global user identity, not a tenant membership. The TOTP secret is protected with ASP.NET Core Data Protection, recovery codes are stored as non-reversible hashes, and the ordinary login request is repeated with the second-factor code.

## Rationale

A user may belong to multiple tenants but has one login identity. Account-level MFA avoids inconsistent protection between memberships. Requiring successful password and second-factor verification before JWT creation keeps partial authentication out of tenant APIs.

## Consequences

The Data Protection key ring becomes production-critical and must be shared by API replicas and included in backup/restore procedures. Recovery codes are shown only once and consumed on successful use.
