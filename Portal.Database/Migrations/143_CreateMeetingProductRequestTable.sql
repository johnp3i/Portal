-- ============================================================
-- Migration 143: Create Meeting Product Request Table
-- ============================================================
-- Purpose: Creates the [sales].[MeetingProductRequest] table —
--          links a meeting to one or more products that the contact
--          is interested in. Enables product-level tracking per meeting.
-- Schema: [sales]
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'MeetingProductRequest'
)
BEGIN
    CREATE TABLE [sales].[MeetingProductRequest]
    (
        [Id]                        INT             IDENTITY(1,1) NOT NULL,
        [MeetingId]                 INT             NOT NULL,
        [ProductId]                 INT             NOT NULL,
        [RequestText]               NVARCHAR(MAX)   NULL,
        [IsActive]                  BIT             NOT NULL CONSTRAINT [DF_MeetingProductRequest_IsActive] DEFAULT (1),
        [IsCancelled]               BIT             NOT NULL CONSTRAINT [DF_MeetingProductRequest_IsCancelled] DEFAULT (0),
        [CancellationTimestamp]     DATETIME        NULL,
        [CancellationDescription]   NVARCHAR(500)   NULL,
        [CreatedAtUtc]              DATETIME        NOT NULL CONSTRAINT [DF_MeetingProductRequest_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_MeetingProductRequest] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_MeetingProductRequest_Meeting] FOREIGN KEY ([MeetingId]) REFERENCES [sales].[Meeting]([Id]),
        CONSTRAINT [FK_MeetingProductRequest_Product] FOREIGN KEY ([ProductId]) REFERENCES [sales].[Product]([Id])
    );

    PRINT 'Created [sales].[MeetingProductRequest] table.';
END
ELSE
BEGIN
    PRINT '[sales].[MeetingProductRequest] already exists.';
END
GO
