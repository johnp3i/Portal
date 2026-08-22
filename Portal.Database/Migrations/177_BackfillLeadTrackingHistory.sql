-- ============================================================
-- Migration 177: Backfill Lead Tracking History for Existing Leads
-- ============================================================
-- Purpose: Seeds [sales].[LeadTrackingHistory] with records for
--          leads that were already in the pipeline before migration 176.
--          Without this, the regression algorithm would incorrectly
--          regress pre-migration leads to stage 1 (New) instead of
--          the appropriate stage based on their actual activity.
--
-- Strategy:
--   1. For each active, non-cancelled meeting linked to a lead,
--      insert a MeetingScheduled (ActionTypeId=1) history record
--   2. For each lead response, insert a ResponseSent (ActionTypeId=4)
--      history record
--   3. For each lead with a linked quotation,
--      insert a ProposalLinked (ActionTypeId=5) history record
--
-- Idempotent: Only inserts records for leads that have NO existing
-- history records (skips leads already tracked by the live system).
-- ============================================================

USE [Portal]
GO

-- Step 1: Backfill MeetingScheduled records for existing active meetings
INSERT INTO [sales].[LeadTrackingHistory]
    ([LeadRequestId], [BusinessId], [LeadTrackingActionTypeId],
     [FromLeadStatusTypeId], [ToLeadStatusTypeId], [RelatedEntityId],
     [CreatedByUserId], [CreatedAtUtc])
SELECT
    [sales].[Meeting].[LeadRequestId],
    [sales].[Meeting].[BusinessId],
    1,    -- MeetingScheduled
    NULL, -- FromLeadStatusTypeId unknown for backfill
    4,    -- ToLeadStatusTypeId = Meetings
    [sales].[Meeting].[Id],
    NULL,
    [sales].[Meeting].[CreatedAtUtc]
FROM [sales].[Meeting]
WHERE [sales].[Meeting].[LeadRequestId] IS NOT NULL
  AND [sales].[Meeting].[IsCancelled] = 0
  AND [sales].[Meeting].[IsActive] = 1
  AND NOT EXISTS (
      SELECT 1 FROM [sales].[LeadTrackingHistory]
      WHERE [sales].[LeadTrackingHistory].[LeadRequestId] = [sales].[Meeting].[LeadRequestId]
        AND [sales].[LeadTrackingHistory].[BusinessId] = [sales].[Meeting].[BusinessId]
  )
GO

PRINT 'Backfilled MeetingScheduled records for existing active meetings.'
GO

-- Step 2: Backfill ResponseSent records for existing responses
INSERT INTO [sales].[LeadTrackingHistory]
    ([LeadRequestId], [BusinessId], [LeadTrackingActionTypeId],
     [FromLeadStatusTypeId], [ToLeadStatusTypeId], [RelatedEntityId],
     [CreatedByUserId], [CreatedAtUtc])
SELECT
    [sales].[LeadResponse].[LeadRequestId],
    [sales].[LeadRequest].[BusinessId],
    4,    -- ResponseSent
    NULL, -- FromLeadStatusTypeId unknown for backfill
    2,    -- ToLeadStatusTypeId = Contacted
    [sales].[LeadResponse].[Id],
    NULL,
    [sales].[LeadResponse].[CreatedAtUtc]
FROM [sales].[LeadResponse]
INNER JOIN [sales].[LeadRequest] ON [sales].[LeadResponse].[LeadRequestId] = [sales].[LeadRequest].[Id]
WHERE NOT EXISTS (
      SELECT 1 FROM [sales].[LeadTrackingHistory]
      WHERE [sales].[LeadTrackingHistory].[LeadRequestId] = [sales].[LeadResponse].[LeadRequestId]
        AND [sales].[LeadTrackingHistory].[BusinessId] = [sales].[LeadRequest].[BusinessId]
        AND [sales].[LeadTrackingHistory].[LeadTrackingActionTypeId] = 4
        AND [sales].[LeadTrackingHistory].[RelatedEntityId] = [sales].[LeadResponse].[Id]
  )
GO

PRINT 'Backfilled ResponseSent records for existing responses.'
GO

-- Step 3: Backfill ProposalLinked records for quotations linked to leads
INSERT INTO [sales].[LeadTrackingHistory]
    ([LeadRequestId], [BusinessId], [LeadTrackingActionTypeId],
     [FromLeadStatusTypeId], [ToLeadStatusTypeId], [RelatedEntityId],
     [CreatedByUserId], [CreatedAtUtc])
SELECT
    [quotation].[Quotation].[LeadRequestId],
    [quotation].[Quotation].[BusinessId],
    5,    -- ProposalLinked
    NULL, -- FromLeadStatusTypeId unknown for backfill
    5,    -- ToLeadStatusTypeId = Proposal
    [quotation].[Quotation].[Id],
    NULL,
    [quotation].[Quotation].[CreatedAtUtc]
FROM [quotation].[Quotation]
WHERE [quotation].[Quotation].[LeadRequestId] IS NOT NULL
  AND [quotation].[Quotation].[IsDeleted] = 0
  AND NOT EXISTS (
      SELECT 1 FROM [sales].[LeadTrackingHistory]
      WHERE [sales].[LeadTrackingHistory].[LeadRequestId] = [quotation].[Quotation].[LeadRequestId]
        AND [sales].[LeadTrackingHistory].[BusinessId] = [quotation].[Quotation].[BusinessId]
        AND [sales].[LeadTrackingHistory].[LeadTrackingActionTypeId] = 5
        AND [sales].[LeadTrackingHistory].[RelatedEntityId] = [quotation].[Quotation].[Id]
  )
GO

PRINT 'Backfilled ProposalLinked records for existing linked quotations.'
GO
