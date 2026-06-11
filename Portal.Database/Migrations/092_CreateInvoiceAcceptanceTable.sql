/*
    Migration: 092_CreateInvoiceAcceptanceTable
    Description: Creates the [invoice].[InvoiceAcceptance] table — stores immutable
                 audit records capturing a customer's formal acceptance of a shared
                 invoice. Each record includes the acceptance terms text, UTC timestamp,
                 client IP address, and user-agent string. A UNIQUE constraint on
                 InvoiceShareId enforces the one-acceptance-per-share invariant.

    Requirements: 6.1 - THE Invoice_Acceptance_System SHALL store the exact Acceptance_Terms text
                  6.2 - THE Invoice_Acceptance_System SHALL store the client IP address
                  6.3 - THE Invoice_Acceptance_System SHALL store the user-agent string
                  6.4 - THE Invoice_Acceptance_System SHALL store the UTC timestamp
                  6.5 - THE Invoice_Acceptance_System SHALL make Acceptance_Record fields immutable
                  3.1 - THE Invoice_Acceptance_System SHALL store at most one Acceptance_Record per InvoiceShare

    This script is idempotent — safe to run multiple times.
*/

-- =============================================================================
-- 1. Create [invoice].[InvoiceAcceptance] table
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'invoice'
      AND TABLE_NAME = 'InvoiceAcceptance'
)
BEGIN
    CREATE TABLE [invoice].[InvoiceAcceptance]
    (
        [Id]              INT                IDENTITY(1,1)  NOT NULL,
        [InvoiceShareId]  INT                               NOT NULL,
        [AcceptedTerms]   NVARCHAR(500)                     NOT NULL,
        [AcceptedAtUtc]   DATETIMEOFFSET                    NOT NULL,
        [IpAddress]       NVARCHAR(45)                      NOT NULL,
        [UserAgent]       NVARCHAR(500)                     NOT NULL,
        [CreatedAtUtc]    DATETIMEOFFSET                    NOT NULL
            CONSTRAINT [DF_InvoiceAcceptance_CreatedAtUtc] DEFAULT (SYSDATETIMEOFFSET()),

        CONSTRAINT [PK_InvoiceAcceptance] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_InvoiceAcceptance_InvoiceShare]
            FOREIGN KEY ([InvoiceShareId]) REFERENCES [invoice].[InvoiceShare] ([Id]),
        CONSTRAINT [UX_InvoiceAcceptance_InvoiceShareId]
            UNIQUE NONCLUSTERED ([InvoiceShareId])
    );
END
GO
