/*
    Migration: 101_AddIsPaymentInstructionsEnabledToBusiness
    Description: Adds IsPaymentInstructionsEnabled BIT column to [portal].[Business]
                 controlling whether bank transfer payment instructions are shown
                 on shared invoice pages. Defaults to 0 (disabled).
    Requirements: 1.4
*/

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[portal].[Business]')
      AND name = N'IsPaymentInstructionsEnabled'
)
BEGIN
    ALTER TABLE [portal].[Business]
        ADD [IsPaymentInstructionsEnabled] BIT NOT NULL
            CONSTRAINT [DF_Business_IsPaymentInstructionsEnabled] DEFAULT (0);
END
GO
