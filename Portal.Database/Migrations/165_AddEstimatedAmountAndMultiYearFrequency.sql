-- ============================================================
-- Add EstimatedAmount to BusinessApplication and ApplicationType
-- Add FrequencyInterval to ApplicationType for Multi-Year frequency
-- ============================================================

USE [Portal]
GO

ALTER TABLE [compliance].[ApplicationType]
    ADD [EstimatedAmount] DECIMAL(18,2) NULL;
GO

ALTER TABLE [compliance].[ApplicationType]
    ADD [FrequencyInterval] INT NULL;
GO

ALTER TABLE [compliance].[BusinessApplication]
    ADD [EstimatedAmount] DECIMAL(18,2) NULL;
GO

-- Update CHECK constraint on Frequency to include 'Multi-Year'
ALTER TABLE [compliance].[ApplicationType]
    DROP CONSTRAINT [CK_ApplicationType_Frequency];
GO

ALTER TABLE [compliance].[ApplicationType]
    ADD CONSTRAINT [CK_ApplicationType_Frequency]
    CHECK ([Frequency] IN ('Monthly', 'Quarterly', 'Annual', 'One-off', 'Multi-Year'));
GO
