USE [Portal];
GO

/*
    Seed: Sample Promo Codes for Development and Testing
    Description: Inserts a mix of email-bound and generic promo codes with various
                 statuses (active, expired, fully redeemed, revoked) and different
                 DurationMonths / MaxRedemptions configurations.

    Prerequisites:
        - [dbo].[PromoCode] table must exist (migration 082)

    Character set for codes: ABCDEFGHJKLMNPQRSTUVWXYZ23456789 (no O, 0, I, l, 1)
    CreatedByUserId: 'system-seed'

    This script is idempotent — safe to run multiple times.
*/

-- =============================================================================
-- 1. Active generic codes (ExpiresAtUtc in the future, not revoked, not full)
-- =============================================================================

-- Active generic: 3 months, max 10 redemptions, 2 used
IF NOT EXISTS (SELECT 1 FROM [dbo].[PromoCode] WHERE [Code] = 'TRYX3MNB')
BEGIN
    INSERT INTO [dbo].[PromoCode]
        ([Code], [DurationMonths], [MaxRedemptions], [CurrentRedemptions], [ExpiresAtUtc], [BoundEmail], [IsRevoked], [CreatedByUserId], [CreatedAtUtc])
    VALUES
        ('TRYX3MNB', 3, 10, 2, DATEADD(MONTH, 6, GETUTCDATE()), NULL, 0, 'system-seed', GETUTCDATE());

    PRINT 'Inserted active generic promo code TRYX3MNB (3 months, 2/10 redeemed)';
END

-- Active generic: 6 months, max 50 redemptions, 0 used
IF NOT EXISTS (SELECT 1 FROM [dbo].[PromoCode] WHERE [Code] = 'HALF6YRS')
BEGIN
    INSERT INTO [dbo].[PromoCode]
        ([Code], [DurationMonths], [MaxRedemptions], [CurrentRedemptions], [ExpiresAtUtc], [BoundEmail], [IsRevoked], [CreatedByUserId], [CreatedAtUtc])
    VALUES
        ('HALF6YRS', 6, 50, 0, DATEADD(MONTH, 12, GETUTCDATE()), NULL, 0, 'system-seed', GETUTCDATE());

    PRINT 'Inserted active generic promo code HALF6YRS (6 months, 0/50 redeemed)';
END

-- Active generic: 12 months, max 5 redemptions, 3 used
IF NOT EXISTS (SELECT 1 FROM [dbo].[PromoCode] WHERE [Code] = 'ANNU8LPK')
BEGIN
    INSERT INTO [dbo].[PromoCode]
        ([Code], [DurationMonths], [MaxRedemptions], [CurrentRedemptions], [ExpiresAtUtc], [BoundEmail], [IsRevoked], [CreatedByUserId], [CreatedAtUtc])
    VALUES
        ('ANNU8LPK', 12, 5, 3, DATEADD(MONTH, 3, GETUTCDATE()), NULL, 0, 'system-seed', GETUTCDATE());

    PRINT 'Inserted active generic promo code ANNU8LPK (12 months, 3/5 redeemed)';
END

-- Active generic: 1 month, max 100 redemptions, 15 used
IF NOT EXISTS (SELECT 1 FROM [dbo].[PromoCode] WHERE [Code] = 'EVNT2W4K')
BEGIN
    INSERT INTO [dbo].[PromoCode]
        ([Code], [DurationMonths], [MaxRedemptions], [CurrentRedemptions], [ExpiresAtUtc], [BoundEmail], [IsRevoked], [CreatedByUserId], [CreatedAtUtc])
    VALUES
        ('EVNT2W4K', 1, 100, 15, DATEADD(MONTH, 2, GETUTCDATE()), NULL, 0, 'system-seed', GETUTCDATE());

    PRINT 'Inserted active generic promo code EVNT2W4K (1 month, 15/100 redeemed)';
END

-- =============================================================================
-- 2. Active email-bound codes (ExpiresAtUtc in the future, MaxRedemptions=1)
-- =============================================================================

-- Active email-bound: 3 months, bound to sarah.mitchell@example.com
IF NOT EXISTS (SELECT 1 FROM [dbo].[PromoCode] WHERE [Code] = 'VKP4SNHR')
BEGIN
    INSERT INTO [dbo].[PromoCode]
        ([Code], [DurationMonths], [MaxRedemptions], [CurrentRedemptions], [ExpiresAtUtc], [BoundEmail], [IsRevoked], [CreatedByUserId], [CreatedAtUtc])
    VALUES
        ('VKP4SNHR', 3, 1, 0, DATEADD(MONTH, 3, GETUTCDATE()), 'sarah.mitchell@example.com', 0, 'system-seed', GETUTCDATE());

    PRINT 'Inserted active email-bound promo code VKP4SNHR (3 months, sarah.mitchell@example.com)';
END

-- Active email-bound: 6 months, bound to james.wilson@contoso.com
IF NOT EXISTS (SELECT 1 FROM [dbo].[PromoCode] WHERE [Code] = 'BN7GFXWT')
BEGIN
    INSERT INTO [dbo].[PromoCode]
        ([Code], [DurationMonths], [MaxRedemptions], [CurrentRedemptions], [ExpiresAtUtc], [BoundEmail], [IsRevoked], [CreatedByUserId], [CreatedAtUtc])
    VALUES
        ('BN7GFXWT', 6, 1, 0, DATEADD(MONTH, 6, GETUTCDATE()), 'james.wilson@contoso.com', 0, 'system-seed', GETUTCDATE());

    PRINT 'Inserted active email-bound promo code BN7GFXWT (6 months, james.wilson@contoso.com)';
END

-- Active email-bound: 12 months, bound to priya.sharma@techcorp.io
IF NOT EXISTS (SELECT 1 FROM [dbo].[PromoCode] WHERE [Code] = 'RJ5DCYML')
BEGIN
    INSERT INTO [dbo].[PromoCode]
        ([Code], [DurationMonths], [MaxRedemptions], [CurrentRedemptions], [ExpiresAtUtc], [BoundEmail], [IsRevoked], [CreatedByUserId], [CreatedAtUtc])
    VALUES
        ('RJ5DCYML', 12, 1, 0, DATEADD(MONTH, 9, GETUTCDATE()), 'priya.sharma@techcorp.io', 0, 'system-seed', GETUTCDATE());

    PRINT 'Inserted active email-bound promo code RJ5DCYML (12 months, priya.sharma@techcorp.io)';
END

-- =============================================================================
-- 3. Expired codes (ExpiresAtUtc in the past, not revoked, not fully redeemed)
-- =============================================================================

-- Expired generic: 3 months, expired 30 days ago, 4/20 redeemed
IF NOT EXISTS (SELECT 1 FROM [dbo].[PromoCode] WHERE [Code] = 'XPRD3GHK')
BEGIN
    INSERT INTO [dbo].[PromoCode]
        ([Code], [DurationMonths], [MaxRedemptions], [CurrentRedemptions], [ExpiresAtUtc], [BoundEmail], [IsRevoked], [CreatedByUserId], [CreatedAtUtc])
    VALUES
        ('XPRD3GHK', 3, 20, 4, DATEADD(DAY, -30, GETUTCDATE()), NULL, 0, 'system-seed', DATEADD(DAY, -120, GETUTCDATE()));

    PRINT 'Inserted expired generic promo code XPRD3GHK (3 months, expired 30 days ago)';
END

-- Expired email-bound: 6 months, expired 14 days ago, unused
IF NOT EXISTS (SELECT 1 FROM [dbo].[PromoCode] WHERE [Code] = 'LTMV9FEW')
BEGIN
    INSERT INTO [dbo].[PromoCode]
        ([Code], [DurationMonths], [MaxRedemptions], [CurrentRedemptions], [ExpiresAtUtc], [BoundEmail], [IsRevoked], [CreatedByUserId], [CreatedAtUtc])
    VALUES
        ('LTMV9FEW', 6, 1, 0, DATEADD(DAY, -14, GETUTCDATE()), 'mark.thompson@expired.co', 0, 'system-seed', DATEADD(DAY, -90, GETUTCDATE()));

    PRINT 'Inserted expired email-bound promo code LTMV9FEW (6 months, mark.thompson@expired.co)';
END

-- Expired generic: 1 month, expired 60 days ago, 8/25 redeemed
IF NOT EXISTS (SELECT 1 FROM [dbo].[PromoCode] WHERE [Code] = 'CNFR4P2Z')
BEGIN
    INSERT INTO [dbo].[PromoCode]
        ([Code], [DurationMonths], [MaxRedemptions], [CurrentRedemptions], [ExpiresAtUtc], [BoundEmail], [IsRevoked], [CreatedByUserId], [CreatedAtUtc])
    VALUES
        ('CNFR4P2Z', 1, 25, 8, DATEADD(DAY, -60, GETUTCDATE()), NULL, 0, 'system-seed', DATEADD(DAY, -150, GETUTCDATE()));

    PRINT 'Inserted expired generic promo code CNFR4P2Z (1 month, expired 60 days ago)';
END

-- =============================================================================
-- 4. Fully redeemed codes (CurrentRedemptions = MaxRedemptions)
-- =============================================================================

-- Fully redeemed generic: 3 months, 10/10 redeemed
IF NOT EXISTS (SELECT 1 FROM [dbo].[PromoCode] WHERE [Code] = 'FULL8TKR')
BEGIN
    INSERT INTO [dbo].[PromoCode]
        ([Code], [DurationMonths], [MaxRedemptions], [CurrentRedemptions], [ExpiresAtUtc], [BoundEmail], [IsRevoked], [CreatedByUserId], [CreatedAtUtc])
    VALUES
        ('FULL8TKR', 3, 10, 10, DATEADD(MONTH, 2, GETUTCDATE()), NULL, 0, 'system-seed', DATEADD(DAY, -45, GETUTCDATE()));

    PRINT 'Inserted fully redeemed generic promo code FULL8TKR (3 months, 10/10)';
END

-- Fully redeemed email-bound: 6 months, 1/1 redeemed
IF NOT EXISTS (SELECT 1 FROM [dbo].[PromoCode] WHERE [Code] = 'USED6NWQ')
BEGIN
    INSERT INTO [dbo].[PromoCode]
        ([Code], [DurationMonths], [MaxRedemptions], [CurrentRedemptions], [ExpiresAtUtc], [BoundEmail], [IsRevoked], [CreatedByUserId], [CreatedAtUtc])
    VALUES
        ('USED6NWQ', 6, 1, 1, DATEADD(MONTH, 4, GETUTCDATE()), 'anna.kowalski@redeemed.net', 0, 'system-seed', DATEADD(DAY, -30, GETUTCDATE()));

    PRINT 'Inserted fully redeemed email-bound promo code USED6NWQ (6 months, anna.kowalski@redeemed.net)';
END

-- Fully redeemed generic: 12 months, 5/5 redeemed
IF NOT EXISTS (SELECT 1 FROM [dbo].[PromoCode] WHERE [Code] = 'DUNE5KYJ')
BEGIN
    INSERT INTO [dbo].[PromoCode]
        ([Code], [DurationMonths], [MaxRedemptions], [CurrentRedemptions], [ExpiresAtUtc], [BoundEmail], [IsRevoked], [CreatedByUserId], [CreatedAtUtc])
    VALUES
        ('DUNE5KYJ', 12, 5, 5, DATEADD(MONTH, 1, GETUTCDATE()), NULL, 0, 'system-seed', DATEADD(DAY, -60, GETUTCDATE()));

    PRINT 'Inserted fully redeemed generic promo code DUNE5KYJ (12 months, 5/5)';
END

-- =============================================================================
-- 5. Revoked codes (IsRevoked = 1)
-- =============================================================================

-- Revoked generic: 3 months, had 3/30 redemptions before revocation
IF NOT EXISTS (SELECT 1 FROM [dbo].[PromoCode] WHERE [Code] = 'RVKD7WSN')
BEGIN
    INSERT INTO [dbo].[PromoCode]
        ([Code], [DurationMonths], [MaxRedemptions], [CurrentRedemptions], [ExpiresAtUtc], [BoundEmail], [IsRevoked], [CreatedByUserId], [CreatedAtUtc])
    VALUES
        ('RVKD7WSN', 3, 30, 3, DATEADD(MONTH, 5, GETUTCDATE()), NULL, 1, 'system-seed', DATEADD(DAY, -20, GETUTCDATE()));

    PRINT 'Inserted revoked generic promo code RVKD7WSN (3 months, revoked with 3/30 used)';
END

-- Revoked email-bound: 12 months, unused before revocation
IF NOT EXISTS (SELECT 1 FROM [dbo].[PromoCode] WHERE [Code] = 'NXPE2HVB')
BEGIN
    INSERT INTO [dbo].[PromoCode]
        ([Code], [DurationMonths], [MaxRedemptions], [CurrentRedemptions], [ExpiresAtUtc], [BoundEmail], [IsRevoked], [CreatedByUserId], [CreatedAtUtc])
    VALUES
        ('NXPE2HVB', 12, 1, 0, DATEADD(MONTH, 8, GETUTCDATE()), 'cancelled.prospect@revoked.com', 1, 'system-seed', DATEADD(DAY, -10, GETUTCDATE()));

    PRINT 'Inserted revoked email-bound promo code NXPE2HVB (12 months, cancelled.prospect@revoked.com)';
END

-- Revoked generic: 6 months, had 12/50 redemptions before revocation
IF NOT EXISTS (SELECT 1 FROM [dbo].[PromoCode] WHERE [Code] = 'STXP4XCR')
BEGIN
    INSERT INTO [dbo].[PromoCode]
        ([Code], [DurationMonths], [MaxRedemptions], [CurrentRedemptions], [ExpiresAtUtc], [BoundEmail], [IsRevoked], [CreatedByUserId], [CreatedAtUtc])
    VALUES
        ('STXP4XCR', 6, 50, 12, DATEADD(MONTH, 4, GETUTCDATE()), NULL, 1, 'system-seed', DATEADD(DAY, -5, GETUTCDATE()));

    PRINT 'Inserted revoked generic promo code STXP4XCR (6 months, revoked with 12/50 used)';
END

GO
