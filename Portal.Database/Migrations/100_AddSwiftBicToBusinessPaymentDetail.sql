/*
    Migration: 100_AddSwiftBicToBusinessPaymentDetail
    Description: Adds optional SWIFT/BIC column to [portal].[BusinessPaymentDetail]
                 for international bank transfers.
    Requirements: 6.1
*/

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[portal].[BusinessPaymentDetail]')
      AND name = N'SwiftBic'
)
BEGIN
    ALTER TABLE [portal].[BusinessPaymentDetail]
        ADD [SwiftBic] NVARCHAR(11) NULL;
END
GO
