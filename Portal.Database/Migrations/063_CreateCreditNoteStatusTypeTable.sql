USE [Portal];
GO

/*
    Migration: 063_CreateCreditNoteStatusTypeTable
    Description: Creates the [credit].CreditNoteStatusType reference table — a system-wide
                 lookup table defining the lifecycle states of a Credit Note.
                 This is a shared reference table with no BusinessId column.

    Requirements: 3.1 - THE Credit_Note_Status_Type reference table SHALL contain exactly
                         four statuses: Draft (1), Issued (2), Applied (3), Voided (4).

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'credit'
      AND TABLE_NAME = 'CreditNoteStatusType'
)
BEGIN
    CREATE TABLE [credit].[CreditNoteStatusType]
    (
        [Id]    INT            NOT NULL,
        [Name]  NVARCHAR(50)   NOT NULL,

        CONSTRAINT [PK_CreditNoteStatusType] PRIMARY KEY CLUSTERED ([Id])
    );
END
GO

-- Seed data: idempotent inserts
IF EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'credit'
      AND TABLE_NAME = 'CreditNoteStatusType'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [credit].[CreditNoteStatusType] WHERE [Id] = 1)
        INSERT INTO [credit].[CreditNoteStatusType] ([Id], [Name]) VALUES (1, 'Draft');

    IF NOT EXISTS (SELECT 1 FROM [credit].[CreditNoteStatusType] WHERE [Id] = 2)
        INSERT INTO [credit].[CreditNoteStatusType] ([Id], [Name]) VALUES (2, 'Issued');

    IF NOT EXISTS (SELECT 1 FROM [credit].[CreditNoteStatusType] WHERE [Id] = 3)
        INSERT INTO [credit].[CreditNoteStatusType] ([Id], [Name]) VALUES (3, 'Applied');

    IF NOT EXISTS (SELECT 1 FROM [credit].[CreditNoteStatusType] WHERE [Id] = 4)
        INSERT INTO [credit].[CreditNoteStatusType] ([Id], [Name]) VALUES (4, 'Voided');
END
GO
