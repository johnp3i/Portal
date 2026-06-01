/*
    Migration: 007_CreatePendingRegistrationTable
    Description: Creates the [membership].[PendingRegistration] table in the Membership database.

                 Tracks a user's selected subscription plan between registration and email
                 confirmation. Once the user confirms their email and completes Stripe checkout,
                 the record is marked as completed.

    This script is idempotent — safe to run multiple times.
*/

-- ============================================================
-- Table: [membership].[PendingRegistration]
-- ============================================================
IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = N'membership'
      AND TABLE_NAME = N'PendingRegistration'
)
BEGIN
    CREATE TABLE [membership].[PendingRegistration] (
        [Id]              INT IDENTITY(1,1) NOT NULL,
        [UserId]          NVARCHAR(450) NOT NULL,
        [PlanId]          INT NOT NULL,
        [IsCompleted]     BIT NOT NULL DEFAULT 0,
        [CreatedAtUtc]    DATETIME NOT NULL DEFAULT GETUTCDATE(),
        [CompletedAtUtc]  DATETIME NULL,
        CONSTRAINT [PK_PendingRegistration] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_PendingRegistration_User] FOREIGN KEY ([UserId])
            REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [UX_PendingRegistration_UserId] UNIQUE ([UserId])
    );
END
GO
