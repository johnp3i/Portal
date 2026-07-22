-- ============================================================
-- Migration 139: Create Lead Request Table
-- ============================================================
-- Purpose: Creates the [sales].[LeadRequest] table — represents an
--          inbound lead enquiry from a contact. Tracks the source,
--          status, optional product interest, and assignment.
-- Schema: [sales]
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'LeadRequest'
)
BEGIN
    CREATE TABLE [sales].[LeadRequest]
    (
        [Id]                            INT             IDENTITY(1,1) NOT NULL,
        [BusinessId]                    INT             NOT NULL,
        [ContactId]                     INT             NOT NULL,
        [ProductId]                     INT             NULL,
        [LeadSourceTypeId]              INT             NOT NULL,
        [LeadSourceReferenceTypeId]     INT             NULL,
        [LeadStatusTypeId]              INT             NOT NULL CONSTRAINT [DF_LeadRequest_LeadStatusTypeId] DEFAULT (1),
        [SourceUrl]                     NVARCHAR(500)   NULL,
        [RequestText]                   NVARCHAR(MAX)   NULL,
        [AssignedToUserId]              NVARCHAR(450)   NULL,
        [IsCancelled]                   BIT             NOT NULL CONSTRAINT [DF_LeadRequest_IsCancelled] DEFAULT (0),
        [CancellationTimestamp]         DATETIME        NULL,
        [CancellationDescription]       NVARCHAR(500)   NULL,
        [IsActive]                      BIT             NOT NULL CONSTRAINT [DF_LeadRequest_IsActive] DEFAULT (1),
        [CreatedAtUtc]                  DATETIME        NOT NULL CONSTRAINT [DF_LeadRequest_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_LeadRequest] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_LeadRequest_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business]([Id]),
        CONSTRAINT [FK_LeadRequest_Contact] FOREIGN KEY ([ContactId]) REFERENCES [sales].[Contact]([Id]),
        CONSTRAINT [FK_LeadRequest_Product] FOREIGN KEY ([ProductId]) REFERENCES [sales].[Product]([Id]),
        CONSTRAINT [FK_LeadRequest_LeadSourceType] FOREIGN KEY ([LeadSourceTypeId]) REFERENCES [sales].[LeadSourceType]([Id]),
        CONSTRAINT [FK_LeadRequest_LeadSourceReferenceType] FOREIGN KEY ([LeadSourceReferenceTypeId]) REFERENCES [sales].[LeadSourceReferenceType]([Id]),
        CONSTRAINT [FK_LeadRequest_LeadStatusType] FOREIGN KEY ([LeadStatusTypeId]) REFERENCES [sales].[LeadStatusType]([Id])
    );

    PRINT 'Created [sales].[LeadRequest] table.';
END
ELSE
BEGIN
    PRINT '[sales].[LeadRequest] already exists.';
END
GO
