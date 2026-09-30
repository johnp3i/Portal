# Testing Scenarios: Orphaned-File Cleanup (Storage Phase 4b)

The orphaned-file cleanup finds files on disk that **no live database row references**, records
them as *candidates* with a scheduled deletion date, and — once switched on — permanently removes
the ones past that date. It has **two independent switches** so detection can run and populate a
report for as long as needed before anything destructive is enabled:

- **Detection** (`OrphanedFileCleanupEnabled`) — the nightly scan that lists candidates. Non-destructive.
- **Deletion** (`OrphanedFileDeletionEnabled`) — the destructive pass that actually deletes due files.

Both ship **disabled**. The admin surface is at `/Admin/Storage/Cleanup` (SuperAdmin only); the
permanent audit trail is at `/Admin/Storage/DeletionReport`.

> ⚠️ **This is the only feature in the platform that deletes user files.** Run these scenarios in a
> non-production environment first, and always work through Part 1–4 (detection + the safety guard)
> before Part 5 (actual deletion).

## Prerequisites

- Run migrations **217–222** against the Portal database (creates the `[Storage]` schema, the
  `OrphanedFileStatusType` / `OrphanedFileCandidate` / `OrphanedFileDeletionLog` tables, seeds the
  status reference rows and the three `PlatformConfig` keys, and fixes the `RelativePath` index key
  length).
- Confirm both switches are **off** and the grace period is the default:
  ```sql
  USE [Portal]
  GO
  SELECT [Key], [Value] FROM [dbo].[PlatformConfig] WHERE [Key] LIKE 'OrphanedFile%';
  -- Expect: OrphanedFileCleanupEnabled = false
  --         OrphanedFileDeletionEnabled = false
  --         OrphanedFileGraceDays = 30
  ```
- Know your storage root: the `FileStorage:BasePath` configuration value. All relative paths below
  are under this root.
- A **SuperAdmin** login (the whole surface is `[Authorize(Roles = "SuperAdmin")]`).
- The business whose time zone drives the page's date display should have a `TimeZoneId` set (dates
  render in that zone; falls back to UTC otherwise).

### Plant test files

Under `{basePath}`, set up three cases by hand so you can verify both true-positive and
true-negative behaviour:

| # | File on disk | DB row? | Should be flagged? |
|---|--------------|---------|--------------------|
| A | `999/document/1/deadbeef_orphan.pdf` | none | **Yes** — true orphan |
| B | a real uploaded attachment (upload through the app) | `DocumentAttachment.StoragePath` matches, live (`IsDeleted = 0`) | **No** — referenced |
| C | a live attachment you will *detach* in Scenario 3b | row will be soft-deleted + tombstoned | **Yes, after detach** — the tombstone releases the file |

---

## Scenario 1: Detection scan lists only true orphans

1. Log in as SuperAdmin, go to `/Admin/Storage/Cleanup`.
2. Click **Scan now** and confirm the dialog.
3. **Expected:** A success message like "Scan complete: N file(s) scanned, 1 orphan candidate(s) recorded. No files were deleted (report-only)."
4. **Expected (table):** Only file **A** (`999/document/1/deadbeef_orphan.pdf`) is listed, status **Pending**, with a **Scheduled deletion** ≈ 30 days out, and Detected/Scheduled dates shown in the business time zone.
5. **Expected (correctness):** Files **B** and **C** are **not** listed. This is the critical check — a false positive here would eventually delete a live or recoverable file.
6. **Expected (banner):** "No files are currently past their scheduled deletion date."

---

## Scenario 2: Scan is idempotent

1. Click **Scan now** a second time.
2. **Expected:** Still exactly one candidate for file A (no duplicate row). `[Storage].[OrphanedFileCandidate]` has a UNIQUE index on `RelativePath`; the scan `MERGE`s rather than inserting blindly.

---

## Scenario 3: Pause and resume

1. On the candidate row, click **Pause**.
2. **Expected:** Status becomes **Paused**; the row now offers **Resume** instead of Pause/Cancel.
3. Click **Resume**.
4. **Expected:** Status returns to **Pending**.

---

## Scenario 3b: Detaching an attachment tombstones it and releases the file for cleanup

This proves the "records are never hard-deleted, but their files still get reclaimed" policy. Use
file **B** from the setup (a live uploaded attachment).

1. Confirm the attachment row is live and its file is on disk:
   ```sql
   USE [Portal]
   GO
   SELECT [Id], [IsDeleted], [StoragePath] FROM [document].[DocumentAttachment]
   WHERE [StoragePath] = N'<file B relative path>';   -- IsDeleted = 0, path NOT starting with 'deleted/'
   ```
2. In the app, **detach/remove** that attachment from its entity (e.g. the purchase/invoice it's on).
3. **Expected (DB):** The row is **still there** (never hard-deleted) but now `IsDeleted = 1`,
   `DeletedAtUtc` is set, and `StoragePath` is rewritten to `deleted/<original path>`:
   ```sql
   SELECT [Id], [IsDeleted], [StoragePath] FROM [document].[DocumentAttachment] WHERE [Id] = <id>;
   -- IsDeleted = 1, StoragePath = 'deleted/1/Purchase/5/…'
   ```
4. **Expected (disk):** The physical file is **still present** (detach doesn't delete it immediately).
5. Click **Scan now** on `/Admin/Storage/Cleanup`.
6. **Expected:** The file is now listed as a **Pending** candidate — the tombstoned row no longer
   protects it (the referenced set excludes `deleted/…` paths), so it has a ~grace-day deletion date.
   From here it follows the same lifecycle as any orphan (Scenarios 6–8: goes due, then gets deleted).

> **Note on the policy:** the DB record is preserved forever (audit), the *path* is tombstoned so it
> stops shielding the file, and the file itself is reclaimed on the normal grace-period cadence.
> There is **no** purchase hard-delete — a purchase is only ever *cancelled* (`IsCancelled`), which
> does not detach its attachments; the tombstone happens when the attachment itself is removed.

---

## Scenario 4: Cancel is a permanent exclusion that survives re-scans

1. Click **Cancel** on the candidate and confirm.
2. **Expected:** Status becomes **Cancelled**.
3. Click **Scan now** again.
4. **Expected:** The row **stays Cancelled** — a cancelled file is a permanent exclusion and is never re-queued to Pending by a scan.
5. Click **Resume** to bring it back to **Pending** for the next scenarios (an admin can un-cancel).

---

## Scenario 5: Grace period edit

1. Set **Grace period (days)** to `1` and click Save.
2. **Expected:** Success message noting it applies to *future* scans. Confirm `OrphanedFileGraceDays = '1'` in `PlatformConfig`.
3. **Note:** Existing candidates keep their original `ScheduledDeletionAtUtc`; grace only affects rows created/refreshed by later scans.
4. Enter `0` (or blank) and Save → **Expected:** rejected with "Grace period must be at least 1 day."

---

## Scenario 6: Make a candidate due

Because grace only applies to new scans, force the existing candidate past its date directly:

```sql
USE [Portal]
GO
UPDATE [Storage].[OrphanedFileCandidate]
SET [ScheduledDeletionAtUtc] = DATEADD(day, -1, GETUTCDATE())
WHERE [RelativePath] = N'999/document/1/deadbeef_orphan.pdf';
```

1. Reload `/Admin/Storage/Cleanup`.
2. **Expected (banner):** "1 file(s) (…) are past their scheduled date and would be deleted by a deletion run now." (the preview count comes from `PreviewDeletionAsync`).

---

## Scenario 7: Safety re-verification — a re-referenced file is spared (most important)

This proves the guard that a file which became referenced again **after** detection is never deleted.

1. Insert a DB row that references file A's exact path:
   ```sql
   -- Use a real attachment shape for your schema; the point is StoragePath = the candidate path.
   INSERT INTO [document].[DocumentAttachment] (/* ...required columns...,*/ [StoragePath])
   VALUES (/* ... */ N'999/document/1/deadbeef_orphan.pdf');
   ```
2. Click **Run deletion now** and confirm the destructive dialog.
3. **Expected:** Result reports **0 deleted, 1 skipped**.
4. **Expected:** File A is **still on disk**; the candidate is back to **Pending**; **nothing** was written to `[Storage].[OrphanedFileDeletionLog]`. `RunCleanupAsync` rebuilds the referenced set at delete time and skips anything now referenced.
5. Delete that DB row again so file A is a true orphan once more.

---

## Scenario 8: Actual deletion (happy path)

1. With file A due (Scenario 6) and truly orphaned again (Scenario 7 step 5), click **Run deletion now** and confirm.
2. **Expected:** Result reports **1 deleted, 0 skipped, N bytes freed**.
3. **Expected (disk):** File A is **gone**.
4. **Expected (DB):** The candidate row is now status **Deleted**.
5. Open `/Admin/Storage/DeletionReport`.
6. **Expected:** One row for file A with reason "Orphaned file removed by cleanup", the file size, and a Deleted time in the business time zone.

---

## Scenario 9: Idempotent deletion / nothing due

1. Click **Run deletion now** again.
2. **Expected:** **0 deleted, 0 skipped** (no due candidates). The pass never touches storage when nothing is due.

---

## Scenario 10: "Already gone" file is logged, not errored

1. Plant a new orphan `999/document/2/ghost.pdf`, **Scan now**, then force it due (Scenario 6 query with the new path).
2. Delete the file from disk manually (leave the candidate row).
3. Click **Run deletion now** and confirm.
4. **Expected:** Counted as **1 deleted, 0 bytes freed** (the file wasn't there), the deletion-log reason is "Orphaned file already absent on disk", and the candidate is marked **Deleted** — no error.

---

## Scenario 11: Enabling automatic deletion requires an explicit warning

1. Toggle **Automatic deletion** on.
2. **Expected:** A strong red SweetAlert2 warning appears ("Enable permanent deletion? … cannot be undone"). Cancel it → the toggle reverts to off and nothing changes.
3. Toggle on again and confirm → `OrphanedFileDeletionEnabled = 'true'` in `PlatformConfig`.
4. Toggle off → back to `'false'`.

---

## Scenario 12: The nightly scheduled job honours both switches

1. Set `OrphanedFileCleanup:ScheduledTimeUtc` to a minute or two ahead and restart the app.
2. **Both switches off:** log shows "Orphaned-file cleanup is disabled … Skipping scan." — no scan.
3. **Detection on, deletion off:** log shows the scan summary, then "Orphaned-file deletion is disabled … Detection-only this run." — candidates recorded, nothing deleted.
4. **Both on:** log shows the scan summary followed by "Nightly orphan deletion: X deleted, Y skipped, Z bytes freed."

---

## Scenario 13: Access control

1. As a non-SuperAdmin, navigate to `/Admin/Storage/Cleanup` (and `/Admin/Storage/DeletionReport`).
2. **Expected:** Access denied — `AdminStorageController` is `[Authorize(Roles = "SuperAdmin")]`.

---

## Cleanup after testing

1. Remove any remaining test files/rows you planted (orphans, the file B/C attachments, the Scenario 7 reference row).
2. Reset config to shipping defaults:
   ```sql
   USE [Portal]
   GO
   UPDATE [dbo].[PlatformConfig] SET [Value] = 'false' WHERE [Key] = 'OrphanedFileCleanupEnabled';
   UPDATE [dbo].[PlatformConfig] SET [Value] = 'false' WHERE [Key] = 'OrphanedFileDeletionEnabled';
   UPDATE [dbo].[PlatformConfig] SET [Value] = '30'    WHERE [Key] = 'OrphanedFileGraceDays';
   ```
3. Optionally clear the test rows from `[Storage].[OrphanedFileCandidate]` and `[Storage].[OrphanedFileDeletionLog]`.

---

## Notes

- **What counts as "referenced".** The scan builds the referenced set from every file-owning table:
  `DocumentAttachment.StoragePath` (**all** rows, including `IsDeleted = 1`),
  `ApplicationAttachment.FilePath`, `Signature.FilePath` (all rows, including inactive), and
  `BusinessLogo` — where the DB stores only the bare filename, so the on-disk path is reconstructed
  as `{BusinessId}/logos/{FileName}`. The app-log folder (`logs/`) is ignored.
- **Fail-safe matching.** Path comparison is case-insensitive and separator-normalised. An empty or
  unknown path is treated as *referenced* — a file is never flagged on ambiguous input.
- **Two switches, on purpose.** Detection and deletion are separate config keys so a full detection
  cycle can be watched (via the candidate list and report) before any file is ever removed.
- **Re-verification is the core guard.** Deletion rebuilds the referenced set *at delete time* and
  re-checks each candidate; a file referenced again since the scan is returned to Pending, not
  deleted. Paused/Cancelled rows are excluded from the due query entirely.
- **Batch resilience.** A per-file delete failure is logged and leaves that candidate Pending to
  retry on the next pass — one bad file never aborts the run. Only files that actually existed on
  disk count toward "bytes freed".
- **Time zone.** All dates on the cleanup page and the deletion report are converted to the current
  admin's business time zone via a single `IBusinessTimeZoneService.GetTimeZoneAsync` lookup.
- **Automated coverage.** `OrphanedFileMatcherTests` (matching logic), `OrphanedFileCleanupDeletionTests`
  (deletion safety invariants), and `RepositorySqlColumnCoverageTests` (raw-SQL column completeness)
  back these scenarios; the manual pass here exercises the disk + DB + UI wiring the unit tests mock out.
