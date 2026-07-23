# Promo Code Tier Selection — Testing Scenarios

## Prerequisites

1. Run migration 153 against the Portal database
2. Ensure `[dbo].[Plan]` has active plans: Starter (Foundation), Professional, Enterprise
3. Log in as SuperAdmin
4. Navigate to `/Admin/PromoCodes`

---

## Scenario 1: Create Promo Code with Professional Tier (Default)

1. Click "Create Promo Code" (or "Show Form")
2. **Expected:** Form shows a "Plan / Tier" dropdown with Professional pre-selected
3. Leave Plan as "Professional", set Duration = 3, Max Redemptions = 1, Expiry = 30 days from now
4. Click "Create"
5. **Expected:** SweetAlert2 success with generated code
6. Check the table row for the new code
7. **Expected:** "Plan" column shows "Professional"

---

## Scenario 2: Create Promo Code with Foundation Tier

1. Open create form
2. Select "Starter" (Foundation) from the Plan dropdown
3. Set Duration = 1, Max Redemptions = 5, Expiry = 60 days
4. Click "Create"
5. **Expected:** Code created successfully
6. **Expected:** Table shows "Starter" in the Plan column

---

## Scenario 3: Create Promo Code with Enterprise Tier

1. Open create form
2. Select "Enterprise" from the Plan dropdown
3. Set Duration = 6, Max Redemptions = 1, Expiry = 14 days, Email = "test@example.com"
4. Click "Create"
5. **Expected:** Code created (email-bound)
6. **Expected:** Table shows "Enterprise" in Plan column, "Email-Bound" in Type column

---

## Scenario 4: Legacy Codes Display "Professional" Default

1. Check existing promo codes created before this feature (PlanId = NULL in DB)
2. **Expected:** Plan column shows "Professional" (the default fallback text)

---

## Scenario 5: Redeem Professional Promo Code

1. Create a promo code with Professional tier selected (Scenario 1)
2. Open a new browser / incognito window
3. Navigate to `/Account/Register`
4. Enter the promo code during registration
5. Complete registration (name, email, password, confirm email)
6. Log in to the new account
7. **Expected:** Sidebar shows all Professional modules (Opportunities, P&L, Cash Flow, Expense Insights, Payment Schedules, etc.)
8. **Expected:** `[billing].[Subscription]` table shows PlanId = Professional plan ID, Status = "trialing"
9. **Expected:** `[membership].[UserBusinessPermission]` has rows for all Professional modules

---

## Scenario 6: Redeem Foundation Promo Code

1. Create a promo code with Foundation (Starter) tier selected
2. Register a new user with this code
3. Log in
4. **Expected:** Sidebar shows only Foundation modules (Customers, Quotations, Invoices, Revenue, Purchases, VAT, Credit Notes, Products)
5. **Expected:** Professional features (Opportunities, P&L, Cash Flow, etc.) are NOT visible
6. Navigate directly to `/Sales/Pipeline`
7. **Expected:** Soft-gate "Feature Upgrade" page shown

---

## Scenario 7: Redeem Enterprise Promo Code

1. Create a promo code with Enterprise tier selected
2. Register a new user with this code
3. Log in
4. **Expected:** Sidebar shows all modules including Enterprise-only features (if implemented: Client Portal, Activity Timeline, API, Webhooks, Multi-Currency)
5. **Expected:** All Professional modules also visible

---

## Scenario 8: Legacy Code Without PlanId Falls Back to Professional

1. Manually insert a promo code in the DB with `PlanId = NULL`:
   ```sql
   INSERT INTO [dbo].[PromoCode] ([Code], [DurationMonths], [MaxRedemptions], [CurrentRedemptions], [ExpiresAtUtc], [IsRevoked], [CreatedByUserId])
   VALUES ('LEGACY01', 3, 1, 0, '2027-01-01', 0, 'system');
   ```
2. Register a new user using code "LEGACY01"
3. Log in
4. **Expected:** User gets Professional tier (fallback behaviour)
5. **Expected:** All Professional modules visible in sidebar

---

## Scenario 9: Plan Dropdown Validation

1. Open create form
2. Verify all three options are present: Starter, Professional, Enterprise
3. **Expected:** No empty/blank option (one must always be selected)
4. **Expected:** Professional is the default selection

---

## Scenario 10: Promo Code Table — Plan Column

1. Create codes with different tiers (Foundation, Professional, Enterprise)
2. View the promo codes table
3. **Expected:** Each code shows its tier name in the "Plan" column
4. **Expected:** Column appears between "Code" and "Type"
5. Filter by status (Active, Redeemed, etc.)
6. **Expected:** Plan column persists across all filter states

---

## Database Verification Checklist

After completing the scenarios, verify in the database:

- [ ] `[dbo].[PromoCode]` has `PlanId` column (INT NULL, FK to Plan)
- [ ] New promo codes have `PlanId` set to the selected plan's ID
- [ ] Legacy codes retain `PlanId = NULL`
- [ ] Redeemed promo codes: check `[billing].[Subscription].PlanId` matches the promo code's PlanId
- [ ] Redeemed promo codes: check `[membership].[UserBusinessPermission]` has correct modules for the tier
- [ ] `[dbo].[PromoCode].CurrentRedemptions` incremented after successful registration

---

## Edge Cases

| Case | Expected Behaviour |
|------|-------------------|
| Plan deleted after code created | FK constraint prevents plan deletion; code remains valid |
| Same code used by two users concurrently | Atomic increment check — second user gets "fully redeemed" |
| Revoked code with valid PlanId | Revoked check happens before PlanId is read — registration blocked |
| Expired code with valid PlanId | Expiry check happens before PlanId is read — registration blocked |
