/*
    Migration: 039_CreateBusinessPaymentDetailTable
    Description: Creates the [portal].[BusinessPaymentDetail] table to store bank account
                 information for a business. Supports multiple accounts per business
                 (e.g., Hellenic Bank, Bank of Cyprus). Displayed on invoice previews.

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'portal' AND TABLE_NAME = 'BusinessPaymentDetail'
)
BEGIN
    CREATE TABLE [portal].[BusinessPaymentDetail]
    (
        [Id]          INT            IDENTITY(1,1) NOT NULL,
        [BusinessId]  INT                          NOT NULL,
        [Label]       NVARCHAR(100)                NOT NULL,
        [BankName]    NVARCHAR(200)                NOT NULL,
        [Iban]        NVARCHAR(50)                 NOT NULL,
        [PayeeName]   NVARCHAR(200)                NOT NULL,
        [SortOrder]   INT                          NOT NULL CONSTRAINT [DF_BusinessPaymentDetail_SortOrder] DEFAULT (0),
        [IsActive]    BIT                          NOT NULL CONSTRAINT [DF_BusinessPaymentDetail_IsActive] DEFAULT (1),
        [CreatedAtUtc] DATETIME2                   NOT NULL CONSTRAINT [DF_BusinessPaymentDetail_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_BusinessPaymentDetail] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_BusinessPaymentDetail_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id])
    );
END
GO
