/*
    Migration: 084_SeedPlatformConfig
    Description: Seeds the [dbo].[PlatformConfig] table with initial configuration
                 records required by the promo code system:
                 1. ShowPromoCodeField — Controls visibility of the promo code field
                    on the registration page (default: false)
                 2. TrialBadgeText — Badge text displayed for promo trial subscriptions
                    in the subscription indicator (default: Trial)

    Requirements: 1.7 - THE [dbo].[PlatformConfig] table SHALL be seeded with two initial
                         records: Key = "ShowPromoCodeField" with Value = "false" and
                         Description = "Controls visibility of the promo code field on
                         the registration page", and Key = "TrialBadgeText" with
                         Value = "Trial" and Description = "Badge text displayed for
                         promo trial subscriptions in the subscription indicator".
                 8.1 - THE platform SHALL provide a PlatformConfigService that reads
                         configuration values from the [dbo].[PlatformConfig] table by key.

    This script is idempotent — safe to run multiple times.
*/

-- =============================================================================
-- 1. Seed ShowPromoCodeField
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM [dbo].[PlatformConfig]
    WHERE [Key] = 'ShowPromoCodeField'
)
BEGIN
    INSERT INTO [dbo].[PlatformConfig] ([Key], [Value], [Description])
    VALUES (
        'ShowPromoCodeField',
        'false',
        'Controls visibility of the promo code field on the registration page'
    );
END
GO

-- =============================================================================
-- 2. Seed TrialBadgeText
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM [dbo].[PlatformConfig]
    WHERE [Key] = 'TrialBadgeText'
)
BEGIN
    INSERT INTO [dbo].[PlatformConfig] ([Key], [Value], [Description])
    VALUES (
        'TrialBadgeText',
        'Trial',
        'Badge text displayed for promo trial subscriptions in the subscription indicator'
    );
END
GO
