/*
    Migration: 013_CreatePaymentTable
    Description: Creates the [revenue].Payment table — a monetary transaction recorded
                 against an Invoice. Payments are never physically deleted; voiding sets
                 IsVoided = 1 (soft-delete pattern).
                 Scoped to a Business tenant with foreign keys to Business, Invoice,
                 and PaymentMethodType.

    Requirements: 6.1 - THE Portal_Database SHALL contain a [revenue].Payment table with columns:
                         Id (PK, int identity), BusinessId (FK to [portal].Business),
                         InvoiceId (FK to [invoice].Invoice),
                         PaymentMethodTypeId (FK to [revenue].PaymentMethodType),
                         PaymentDateUtc (datetime2), Amount (decimal(18,2)),
                         Reference (nvarchar, nullable), Notes (nvarchar(max), nullable),
                         IsVoided (bit, default 0), CreatedAtUtc (datetime2),
                         CreatedByUserId (nvarchar, nullable)
                 6.3 - THE Portal_Database SHALL NOT allow deletion of Payment records;
                         voiding is achieved by setting IsVoided to 1

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'revenue'
      AND TABLE_NAME = 'Payment'
)
BEGIN
    CREATE TABLE [revenue].[Payment]
    (
        [Id]                   INT            IDENTITY(1,1)  NOT NULL,
        [BusinessId]           INT                           NOT NULL,
        [InvoiceId]            INT                           NOT NULL,
        [PaymentMethodTypeId]  INT                           NOT NULL,
        [PaymentDateUtc]       DATETIME2                     NOT NULL,
        [Amount]               DECIMAL(18,2)                 NOT NULL,
        [Reference]            NVARCHAR(200)                 NULL,
        [Notes]                NVARCHAR(MAX)                 NULL,
        [IsVoided]             BIT                           NOT NULL  CONSTRAINT [DF_Payment_IsVoided] DEFAULT (0),
        [CreatedAtUtc]         DATETIME2                     NOT NULL  CONSTRAINT [DF_Payment_CreatedAtUtc] DEFAULT (GETUTCDATE()),
        [CreatedByUserId]      NVARCHAR(450)                 NULL,

        CONSTRAINT [PK_Payment] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_Payment_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id]),
        CONSTRAINT [FK_Payment_Invoice] FOREIGN KEY ([InvoiceId]) REFERENCES [invoice].[Invoice] ([Id]),
        CONSTRAINT [FK_Payment_PaymentMethodType] FOREIGN KEY ([PaymentMethodTypeId]) REFERENCES [revenue].[PaymentMethodType] ([Id])
    );
END
GO

-- Non-clustered index on BusinessId for tenant-filtered query optimisation
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_Payment_BusinessId'
      AND [object_id] = OBJECT_ID('[revenue].[Payment]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Payment_BusinessId]
        ON [revenue].[Payment] ([BusinessId]);
END
GO
