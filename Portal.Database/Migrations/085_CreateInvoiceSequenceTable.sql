USE [Portal];
GO

/*
    Migration: 085_CreateInvoiceSequenceTable
    Description: Creates the [billing].[InvoiceSequence] table — a persistent sequence
                 counter that tracks the last assigned invoice number per calendar year.
                 Used by the InvoiceNumberGenerator to produce unique, sequential invoice
                 numbers in the format {PlatformCode}-INV-{yyyy}-{NNNN}. Includes a CHECK
                 constraint to ensure LastNumber never goes negative.

    Requirements: 2.1 - THE Sequence_Counter SHALL be stored in a dedicated
                        [billing].[InvoiceSequence] table with columns: Year (INT, NOT NULL,
                        primary key), LastNumber (INT, NOT NULL, default 0), and CreatedAtUtc
                        (DATETIME, NOT NULL, default GETUTCDATE()).

    This script is idempotent — safe to run multiple times.
*/

-- =============================================================================
-- 1. Create [billing].[InvoiceSequence]
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'billing'
      AND TABLE_NAME = 'InvoiceSequence'
)
BEGIN
    CREATE TABLE [billing].[InvoiceSequence]
    (
        [Year]          INT         NOT NULL,
        [LastNumber]    INT         NOT NULL  CONSTRAINT [DF_InvoiceSequence_LastNumber] DEFAULT (0),
        [CreatedAtUtc]  DATETIME    NOT NULL  CONSTRAINT [DF_InvoiceSequence_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_InvoiceSequence] PRIMARY KEY CLUSTERED ([Year]),
        CONSTRAINT [CK_InvoiceSequence_LastNumber] CHECK ([LastNumber] >= 0)
    );
END
GO
