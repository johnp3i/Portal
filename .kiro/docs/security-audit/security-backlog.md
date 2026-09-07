# Security Backlog

Running list of known security issues to address, in priority order. Items are added as they
are discovered (e.g. incidentally during feature work) and removed once fixed and verified.

Severity scale: **Critical** > **High** > **Medium** > **Low**.

---

## Open

### SEC-1 — Plaintext SMTP credentials in `appsettings.json`

- **Severity:** High
- **Discovered:** 24 Aug 2026, incidentally during Digital Assistants Group 3 work.
- **File:** `Portal.Web/appsettings.json` → `EmailAccounts[]` entries.
- **Issue:** SMTP account passwords are stored in cleartext in `appsettings.json`, which is
  committed to source control. At least the `notifications@3inventors.com` account has a
  literal password in the `Password` field; other `EmailAccounts` entries should be checked
  too. Anyone with repository read access (or access to a deployed config file) obtains live
  mail-server credentials, enabling spoofed mail from the company's domain and reputation
  damage.
- **Recommended fix:**
  - Move every `EmailAccounts:*:Password` out of `appsettings.json` into **User Secrets**
    (development) and a secure secret store / environment variables (production), matching the
    approach already used for the `Notifications` account password per the Phase 1 setup.
  - Replace the committed values with empty strings or remove the keys so config binding still
    works but no secret is present.
  - **Rotate** the exposed mail passwords after removal, since they have been in source control.
  - Audit git history; consider the exposed credentials compromised regardless of removal.
- **Notes:** Left untouched during Group 3 to avoid scope creep; flagged for a dedicated fix.

---

## Resolved

### SEC-2 — Notification failure-alert throttle is process-local (resets on restart)

- **Severity:** Low
- **Discovered:** 24 Aug 2026, noted in code during Digital Assistants work.
- **Resolved:** 24 Aug 2026.
- **File:** `Portal.Web/Services/Notifications/NotificationAdminAlertService.cs`.
- **Issue:** The admin failure-alert throttle (`_lastAlertUtc`) was a static, per-process field.
  On app restart it reset, so a persistent delivery failure could re-alert within the intended
  window, and in a multi-instance deployment each instance throttled independently (duplicate
  alerts). Operational/alerting-hygiene issue, not data exposure.
- **Fix applied:** Replaced the static field + lock with a durable timestamp persisted in
  `[dbo].[PlatformConfig]` under the key `NotificationLastFailureAlertUtc` (ISO-8601 round-trip
  UTC). The throttle now reads the stored timestamp, compares against the window, and writes the
  new timestamp **before** sending (claiming the window so a concurrent pass or the next poll
  does not re-alert). Survives restarts and is shared across instances. An unset/unparseable
  value is treated as "never alerted" (fails safe toward alerting, never silent suppression).

_See `2026-07-24_tenant-isolation-audit.md` for previously fixed tenant-isolation issues._
