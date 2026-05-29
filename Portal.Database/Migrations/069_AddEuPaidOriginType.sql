/*
    Migration: 069_AddEuPaidOriginType
    Description: Adds the "EuPaid" entry (Id=4) to the [purchase].[PurchaseOriginType]
                 lookup table. This origin type represents EU purchases where VAT was
                 actually charged and paid, distinct from EU Reverse Charge (Id=2) where
                 VAT is zero.

    Requirements: 1.1 - THE Portal_System SHALL provide a PurchaseOriginType entry with
                         Id=4 and Name="EuPaid"
                  1.6 - WHEN a purchase is saved with origin type "EU Paid", THE Portal_System
                         SHALL persist the PurchaseOriginTypeId as 4

    This script is idempotent — safe to run multiple times.
*/

-- Insert EuPaid origin type (Id=4) if it does not already exist
IF NOT EXISTS (SELECT 1 FROM [purchase].[PurchaseOriginType] WHERE [Id] = 4)
    INSERT INTO [purchase].[PurchaseOriginType] ([Id], [Name]) VALUES (4, 'EuPaid');
GO
