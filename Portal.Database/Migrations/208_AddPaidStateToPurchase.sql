-- ============================================================
-- Migration 208: Add paid-state to Purchase
-- ============================================================
-- Purpose: Track whether a supplier purchase/expense has been paid, so paid
--          purchases stop appearing as "upcoming supplier payments" in the
--          dashboard widget, the Weekly Outstanding Balance digest, and the
--          Daily Brief. Previously a Purchase had due dates but NO paid signal,
--          so the only way to stop the nag was to cancel it (semantically wrong).
--
--          [IsPaid]     BIT      NOT NULL DEFAULT 0  (Is-prefix bit-flag convention)
--          [PaidAtUtc]  DATETIME NULL               (when it was marked paid)
--
--          Additive + defaulted/nullable -> existing rows unaffected (unpaid).
-- Schema: [purchase]
-- ============================================================

USE [Portal]
GO

IF COL_LENGTH('purchase.Purchase', 'IsPaid') IS NULL
BEGIN
    ALTER TABLE [purchase].[Purchase]
        ADD [IsPaid] BIT NOT NULL CONSTRAINT [DF_Purchase_IsPaid] DEFAULT (0);
    PRINT 'Added [IsPaid] to [purchase].[Purchase].';
END
ELSE
    PRINT '[IsPaid] already exists.';
GO

IF COL_LENGTH('purchase.Purchase', 'PaidAtUtc') IS NULL
BEGIN
    ALTER TABLE [purchase].[Purchase]
        ADD [PaidAtUtc] DATETIME NULL;
    PRINT 'Added [PaidAtUtc] to [purchase].[Purchase].';
END
ELSE
    PRINT '[PaidAtUtc] already exists.';
GO
