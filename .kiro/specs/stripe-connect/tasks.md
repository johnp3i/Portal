# Stripe Connect — Tasks

## Phase 1: Foundation

- [x] 1. Create database migration: `[stripe].[ConnectedAccount]` table
- [x] 2. Create database migration: `[stripe].[CheckoutSession]` table
- [x] 3. Create database migration: Seed "Card" payment method type
- [x] 4. Create EF Core entities: `StripeConnectedAccount`, `StripeCheckoutSession`
- [x] 5. Register entities in PortalDbContext with configuration

## Phase 2: Service Layer

- [x] 6. Create `IStripeConnectService` interface
- [x] 7. Create `StripeConnectService` implementation (OAuth, Checkout, Webhook handling)
- [x] 8. Create `StripeConnectRepository` for data access
- [x] 9. Register services in Program.cs DI
- [x] 10. Add Stripe configuration to User Secrets structure

## Phase 3: Business Onboarding

- [x] 11. Add OAuth connect URL generation endpoint (`GET /MyBusiness/StripeConnect`)
- [x] 12. Add OAuth callback handler (`GET /MyBusiness/StripeConnectCallback`)
- [x] 13. Add disconnect endpoint (`POST /MyBusiness/AxPostDisconnectStripe`)
- [x] 14. Add Stripe Connect section to Business Settings view (status, connect/disconnect buttons)

## Phase 4: Payment Flow

- [x] 15. Add `CreateCheckoutSession` endpoint to InvoiceViewController
- [x] 16. Add "Pay by Card" button to shared invoice page (conditional on Stripe connected)
- [x] 17. Add success/cancel URL handling on shared invoice page

## Phase 5: Webhook & Reconciliation

- [x] 18. Create `StripeConnectWebhookController` with signature verification
- [x] 19. Implement `checkout.session.completed` handler (create Payment, recalculate status)
- [x] 20. Implement Stripe fee retrieval from BalanceTransaction (store on CheckoutSession)
- [x] 21. Add idempotency check (prevent duplicate payment creation)
- [x] 22. Trigger receipt auto-generation on webhook payment creation

## Phase 6: Card Payments View (Fee Transparency)

- [x] 23. Create CardPayments controller action (`/Revenue/CardPayments`)
- [x] 24. Create CardPayments Razor view (summary cards + table)
- [x] 25. Add date range filter and pagination
- [x] 26. Add CSV export for card payment transactions
- [x] 27. Add "Card Payments" nav link in Revenue section (conditional on Stripe connected)

## Phase 7: Polish & Verification

- [x] 28. Add plan permission gate (`stripe_connect` module key — Professional+)
- [x] 29. Add Stripe payment badge/icon in payment history views
- [x] 30. Handle error states (account restricted, checkout creation failure)
- [x] 31. Build verify and end-to-end test flow
