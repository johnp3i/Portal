# Review — Prospecting mobile responsiveness

Verdict: APPROVED

Scope reviewed: commit `b8cf95a` ("feat: mobile responsiveness for Prospect Activity and
My Development"), which is HEAD. The task changes are committed, not in the working tree;
the large uncommitted `git diff` is unrelated Sales/Purchase work and out of scope.

Files in the commit (7): Activity.cshtml, Campaigns.cshtml, Import.cshtml,
MyDevelopment.cshtml, ProspectDetail.cshtml, Prospects.cshtml, js/sales/prospecting.js.
No backend, DB, migration, or `.cs`/`.sql` files touched.

## Task 1 — Prospect Activity native card layout (option A, mirrors Purchases)

prospecting.js (activity-log row render): all 9 `<td>` cells now carry a `data-mobile`
attribute, in thead order:
1. When → `data-mobile="when"`
2. Prospect → `data-mobile="prospect"`
3. Campaign → `data-mobile="hide"`
4. Type → `data-mobile="type"`
5. Outcome → `data-mobile="outcome"`
6. Signal → `data-mobile="signal"`
7. Next action → `data-mobile="next"`
8. By → `data-mobile="hide"`
9. Open link → `data-mobile="open"`

Cell inner content and order are unchanged; only attributes were added, so desktop
rendering is byte-for-byte equivalent (data-mobile is referenced only inside the 768px
block). The attribute convention follows Purchase/Index.cshtml (hide/amount/status/actions
there; here the analogous named set).

Activity.cshtml `@@media (max-width: 768px)` block, scoped to `#activityTable` /
`#activitySummary`:
- Rows become white cards: `background:#fff`, `border:1px solid rgba(13,94,166,.10)`,
  `border-radius:16px`, `box-shadow:0 1px 3px rgba(11,27,40,.05)`, `padding:16px`,
  `margin-bottom:12px` — matches the Purchase/Index `#purchaseTable` card recipe.
- `thead` hidden (`display:none !important`).
- Generic mobile.css `.data-table` rules (`min-width:max-content`, `white-space:nowrap`)
  are cancelled: `#activityTable, #activityTable tbody { min-width:0 !important;
  max-width:100% !important }` and `#activityTable tbody td { white-space:normal
  !important; min-width:0 !important }`. The `#id` + `!important` specificity correctly
  overrides mobile.css's `.data-table`-class rules (confirmed those rules exist in
  mobile.css ~lines 1053/1068/1080).
- Grid card mapping: Prospect title + When top-right muted (row 1), Type + Signal pills
  (row 2), Outcome (row 3), Next action (row 4), Open as full-width footer action (row 5);
  Campaign + By hidden via `data-mobile="hide"`.
- `#activitySummary` becomes a 2-column grid (`display:grid; grid-template-columns:1fr 1fr`)
  with `.stat { flex:none !important }`.

RZ1034 check: the `<style>` block (and its comments) contains no literal HTML tag text —
no `<td>`, `<tr>`, `<page-head>`, `<page-body>`, etc. All `<...>` matches in the file are
in the HTML body above the style block. No Razor parse risk.

## Task 2 — MyDevelopment.cshtml

New `@@media(max-width:768px)` block added after the existing 980px block (980px block
untouched):
- `.dev-summary` → `display:grid; grid-template-columns:1fr 1fr; gap:12px` (clean 2-col),
  `.dev-stat { flex:none }`, value font reduced to 24px.
- `.bar-row` → `grid-template-columns:118px 1fr 44px` (down from the desktop
  `186px 1fr 52px`), so the criterion label column shrinks and the bar row no longer
  overflows on narrow screens. Everything else intact.

No literal HTML tag text in the style block. `@@media` escaping correct.

## Constraints

- Conventions: inline `<style>`, vanilla JS (no jQuery) — followed.
- Razor escaping: all new media queries use `@@media` so Razor emits literal `@media`.
- Cache-bust: all 5 Prospecting views reference `prospecting.js?v=17`; grep confirms zero
  remaining `?v=16` references (Activity, Campaigns, Import, ProspectDetail, Prospects).
- Desktop (>768px): no desktop rules changed; new behavior is confined to the new
  768px blocks (and the pre-existing 980px block, untouched).
- Build: commit message records `dotnet build Portal.Web.csproj` → Build succeeded,
  1220 warnings, 0 Errors. Per instructions the build was not re-run.

## Findings

None. No blockers.
