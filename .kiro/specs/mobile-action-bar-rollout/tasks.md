# Tasks: Mobile Action Bar Rollout

> Reference: `.kiro/docs/mobile-action-bar-pattern.md` (verbatim code + steps),
> `.kiro/specs/mobile-action-bar-rollout/design.md` (per-page decisions).
> Each page task = apply pattern, build (0 error CS/RZ; MSB3021 is environmental), manual visual check.

## Phase A — Shared foundation (do first)

- [ ] 1. Move action-bar CSS from `Purchase/Index.cshtml` into `mobile.css`
  - Base rules (`.mobile-action-bar`, `.mab-more-panel`, `.mab-more-item` + hovers) near topbar/filter rules.
  - Mobile `@media (max-width:768px)` rules (`.mab-btn/.mab-primary/.mab-ghost/.mab-count/.mab-more`, `.topbar-actions-desktop{display:none}`).
  - _Req: 1.1, 1.5_

- [ ] 2. Add shared `.mobile-filter-card` rule to `mobile.css` (replaces per-page `.purchase-filter-card`); include the `.mobile-filter-card .filter-panel.expanded` card look.
  - _Req: 1.2_

- [ ] 3. Generalize the More-menu open/close in `mobile-nav.js` (delegated: `.mab-more-toggle` toggles sibling `.mab-more-panel` `.open`; click-outside `.mab-more` closes; sync `aria-expanded`). Remove need for per-page `onclick`/script.
  - _Req: 1.3_

- [ ] 4. Refactor `Purchase/Index.cshtml` to consume shared CSS/JS: delete moved `<style>` rules and the `togglePurchaseMoreMenu` script + `onclick`; rename `purchase-filter-card` → `mobile-filter-card`. Keep page-specific rules (origin grid, `#purchaseTable` card layout).
  - _Req: 1.4_

- [ ] 5. Build + verify Purchases is visually/behaviourally identical (regression baseline). 0 error CS/RZ.
  - _Req: 1.4, 4.1, 4.2, 4.3_

## Phase B — High-traffic full-bar pages

- [ ] 6. Invoice (`Invoice/Index`): action bar (Create + Filters `invoiceFilterPanel` + More exports), `mobile-filter-card`, remove in-card toggle, count. Build + verify.
  - _Req: 2, 4_

- [ ] 7. Quotation (`Quotation/Index`): same, panel `quotationFilterPanel`. Build + verify.
  - _Req: 2, 4_

- [ ] 8. Customer (`Customer/Index`): same, panel `customerFilterPanel`. Build + verify.
  - _Req: 2, 4_

- [ ] 9. CreditNote (`CreditNote/Index`): **wire panel first** (add id + `aria-controls`), then action bar. Build + verify.
  - _Req: 2, 3, 4_

- [ ] 10. Supplier (`Supplier/Index`): **wire panel first**, then action bar. Build + verify.
  - _Req: 2, 3, 4_

- [ ] 11. Revenue Receivables (`Revenue/Receivables`): verify panel wiring; action bar (reduced if no create). Build + verify.
  - _Req: 2, 3, 4_

- [ ] 12. Sales Meetings (`Sales/Meetings`): align existing restructure to the shared pattern (Schedule Meeting primary). Build + verify.
  - _Req: 2, 4_

## Phase C — Remaining list / admin pages (reduced or full, as applicable)

- [ ] 13. Sales Contacts (`Sales/Contacts`) + Sales Tasks (`Sales/Tasks`): verify panels; action bars. Build + verify each.
  - _Req: 2, 3, 4_

- [ ] 14. ZReport (`ZReport/Index`): full bar (New Z-Report primary; import/export in More). Build + verify.
  - _Req: 2, 4_

- [ ] 15. PromoCode (`PromoCode/Index`), Admin Users (`Admin/Index`): action bars (confirm primary per page). Build + verify each.
  - _Req: 2, 4_

- [ ] 16. Filters-only pages: SystemLogs, Audit, Attachment, PaymentReminder/History, SalesImport/Records. Add `[ Filters | More? ]` bar (no primary). Build + verify each.
  - _Req: 2, 4_

- [ ] 17. Vat Periods (`Vat/Index`): **wire panel first**, then bar. Build + verify.
  - _Req: 2, 3, 4_

## Phase D — Wrap-up

- [ ] 18. Update `.kiro/docs/mobile-action-bar-pattern.md` to reflect the now-global CSS/JS: change Step 6 to "CSS is shared in mobile.css — no per-page CSS" and Step 5 to "no per-page JS (delegated handler)".
  - _Req: 1_

- [ ] 19. Final pass: confirm no in-scope page regressed on desktop; list any pages intentionally left excluded and why (update requirements §5.3 if the set changed during implementation).
  - _Req: 4, 5_
