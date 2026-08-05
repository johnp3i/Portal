-- ============================================================
-- Phase C: PayslipEmailLog table for tracking payslip email sends
-- Handles both fresh creation AND upgrade from Phase A schema
-- ============================================================

USE [Portal]
GO

-- ============================================================
-- 1. Create table if it doesn't exist
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PayslipEmailLog' AND schema_id = SCHEMA_ID('payroll'))
BEGIN
    CREATE TABLE [payroll].[PayslipEmailLog] (
        [Id]              INT             IDENTITY(1,1) NOT NULL,
        [PayslipId]       INT             NOT NULL,
        [SentByUserId]    NVARCHAR(450)   NOT NULL,
        [SentToEmail]     NVARCHAR(256)   NOT NULL,
        [SentAtUtc]       DATETIME        NOT NULL CONSTRAINT [DF_PayslipEmailLog_SentAtUtc] DEFAULT (GETUTCDATE()),
        [IsSuccess]       BIT             NOT NULL,
        [FailureReason]   NVARCHAR(500)   NULL,
        [CreatedAtUtc]    DATETIME        NOT NULL CONSTRAINT [DF_PayslipEmailLog_CreatedAtUtc] DEFAULT (GETUTCDATE()),
        CONSTRAINT [PK_PayslipEmailLog] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_PayslipEmailLog_Payslip] FOREIGN KEY ([PayslipId]) REFERENCES [payroll].[Payslip]([Id])
    )
    PRINT 'Created [payroll].[PayslipEmailLog] table.'
END
GO

-- ============================================================
-- 2. Upgrade from Phase A schema: Add IsSuccess if missing
-- ============================================================
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PayslipEmailLog' AND schema_id = SCHEMA_ID('payroll'))
   AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('[payroll].[PayslipEmailLog]') AND name = 'IsSuccess')
BEGIN
    -- Add IsSuccess column (default to 1 for existing records — they were successful sends)
    ALTER TABLE [payroll].[PayslipEmailLog]
        ADD [IsSuccess] BIT NOT NULL CONSTRAINT [DF_PayslipEmailLog_IsSuccess_Temp] DEFAULT (1)
    
    PRINT 'Added [IsSuccess] column to [payroll].[PayslipEmailLog].'
END
GO

-- ============================================================
-- 3. Upgrade from Phase A schema: Add FailureReason if missing
-- ============================================================
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PayslipEmailLog' AND schema_id = SCHEMA_ID('payroll'))
   AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('[payroll].[PayslipEmailLog]') AND name = 'FailureReason')
BEGIN
    ALTER TABLE [payroll].[PayslipEmailLog]
        ADD [FailureReason] NVARCHAR(500) NULL
    
    PRINT 'Added [FailureReason] column to [payroll].[PayslipEmailLog].'
END
GO

-- ============================================================
-- 4. Drop old IsSignatureIncluded column if it exists (Phase A leftover)
--    Must drop the default constraint first
-- ============================================================
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('[payroll].[PayslipEmailLog]') AND name = 'IsSignatureIncluded')
BEGIN
    -- Find and drop the default constraint on IsSignatureIncluded (system-generated name)
    DECLARE @constraintName NVARCHAR(200)
    SELECT @constraintName = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    WHERE dc.parent_object_id = OBJECT_ID('[payroll].[PayslipEmailLog]')
      AND c.name = 'IsSignatureIncluded'

    IF @constraintName IS NOT NULL
    BEGIN
        EXEC('ALTER TABLE [payroll].[PayslipEmailLog] DROP CONSTRAINT [' + @constraintName + ']')
        PRINT 'Dropped default constraint on [IsSignatureIncluded]: ' + @constraintName
    END

    -- Now drop the column
    ALTER TABLE [payroll].[PayslipEmailLog]
        DROP COLUMN [IsSignatureIncluded]
    
    PRINT 'Dropped obsolete [IsSignatureIncluded] column from [payroll].[PayslipEmailLog].'
END
GO

-- ============================================================
-- 5. Ensure SentToEmail column has correct max length (256)
-- Phase A may have used NVARCHAR(200)
-- ============================================================
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('[payroll].[PayslipEmailLog]') AND name = 'SentToEmail' AND max_length < 512)
BEGIN
    ALTER TABLE [payroll].[PayslipEmailLog]
        ALTER COLUMN [SentToEmail] NVARCHAR(256) NOT NULL
    
    PRINT 'Updated [SentToEmail] max length to 256.'
END
GO

-- ============================================================
-- 6. Drop temp default constraint if it was added during upgrade
-- ============================================================
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_PayslipEmailLog_IsSuccess_Temp')
BEGIN
    ALTER TABLE [payroll].[PayslipEmailLog]
        DROP CONSTRAINT [DF_PayslipEmailLog_IsSuccess_Temp]
    
    PRINT 'Dropped temporary default constraint.'
END
GO

-- ============================================================
-- 7. Create index (idempotent)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PayslipEmailLog_PayslipId' AND object_id = OBJECT_ID('[payroll].[PayslipEmailLog]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PayslipEmailLog_PayslipId]
    ON [payroll].[PayslipEmailLog] ([PayslipId])
    INCLUDE ([SentAtUtc], [IsSuccess])
    
    PRINT 'Created index [IX_PayslipEmailLog_PayslipId].'
END
GO
