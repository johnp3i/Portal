-- ============================================================
-- Add IsOnboardingDismissed flag to Business table
-- ============================================================

USE [Portal]
GO

ALTER TABLE [portal].[Business]
ADD [IsOnboardingDismissed] BIT NOT NULL CONSTRAINT [DF_Business_IsOnboardingDismissed] DEFAULT 0;
GO
