/*
    Migration: 226_CreateProspectActivitySelfEvaluation
    Description: Creates [sales].[ActivitySelfEvaluation] — a private, author-only personal-learning
                 record attached to a prospect activity (Call Reflection / Self-Evaluation module).

                 This is a SEPARATE data stream from the commercial activity. It never changes the
                 prospect status, qualification/ICP score, interaction signal, or lead conversion,
                 and it is author-only: a reflection belongs to the user who wrote it and is not
                 visible to other users in the tenant or to other tenants.

                 Six optional 1-5 criterion ratings (NULL = not assessed, which is distinct from a
                 score of 1), three free-text reflection prompts, and optional pre-call intention
                 fields. The self-assessment average is computed in application logic from the rated
                 criteria only, so no aggregate column is stored.

                 Lives in [sales] beside its FK parent [sales].[ProspectActivity] rather than a
                 separate schema, to keep the one-to-one relationship and the tenant pattern intact.

    This script is idempotent — safe to run multiple times.
*/

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = N'sales' AND t.name = N'ActivitySelfEvaluation'
)
BEGIN
    CREATE TABLE [sales].[ActivitySelfEvaluation] (
        [Id]                    INT IDENTITY(1,1) NOT NULL,
        [BusinessId]            INT NOT NULL,
        [ProspectActivityId]    INT NOT NULL,
        -- The author of the reflection (ASP.NET Identity user id). Author-only privacy is enforced
        -- against this column, independent of the activity's own PerformedByUserId.
        [EvaluatorUserId]       NVARCHAR(450) NOT NULL,

        -- Six self-assessment criteria. NULL = "Not assessed" (distinct from a rating of 1).
        [OpeningCredibility]    TINYINT NULL,
        [ListeningDiscovery]    TINYINT NULL,
        [Adaptability]          TINYINT NULL,
        [ClarityRelevance]      TINYINT NULL,
        [ConversationControl]   TINYINT NULL,
        [NextStepAgreement]     TINYINT NULL,

        -- Free-text reflection prompts.
        [Observations]          NVARCHAR(2000) NULL,
        [Strengths]             NVARCHAR(2000) NULL,
        [NextImprovement]       NVARCHAR(2000) NULL,

        -- Optional pre-call intention (recorded without fabricating a completed call).
        [PreCallObjective]      NVARCHAR(1000) NULL,
        [PreCallFocus]          NVARCHAR(1000) NULL,

        [CreatedAtUtc]          DATETIME NOT NULL CONSTRAINT [DF_ActivitySelfEvaluation_CreatedAtUtc] DEFAULT (GETUTCDATE()),
        [UpdatedAtUtc]          DATETIME NULL,

        CONSTRAINT [PK_ActivitySelfEvaluation] PRIMARY KEY CLUSTERED ([Id] ASC),

        CONSTRAINT [FK_ActivitySelfEvaluation_Business]
            FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id]),

        CONSTRAINT [FK_ActivitySelfEvaluation_ProspectActivity]
            FOREIGN KEY ([ProspectActivityId]) REFERENCES [sales].[ProspectActivity] ([Id]),

        -- Ratings must be 1-5 when present.
        CONSTRAINT [CK_ActivitySelfEvaluation_OpeningCredibility]  CHECK ([OpeningCredibility]  IS NULL OR [OpeningCredibility]  BETWEEN 1 AND 5),
        CONSTRAINT [CK_ActivitySelfEvaluation_ListeningDiscovery]  CHECK ([ListeningDiscovery]  IS NULL OR [ListeningDiscovery]  BETWEEN 1 AND 5),
        CONSTRAINT [CK_ActivitySelfEvaluation_Adaptability]        CHECK ([Adaptability]        IS NULL OR [Adaptability]        BETWEEN 1 AND 5),
        CONSTRAINT [CK_ActivitySelfEvaluation_ClarityRelevance]    CHECK ([ClarityRelevance]    IS NULL OR [ClarityRelevance]    BETWEEN 1 AND 5),
        CONSTRAINT [CK_ActivitySelfEvaluation_ConversationControl] CHECK ([ConversationControl] IS NULL OR [ConversationControl] BETWEEN 1 AND 5),
        CONSTRAINT [CK_ActivitySelfEvaluation_NextStepAgreement]   CHECK ([NextStepAgreement]   IS NULL OR [NextStepAgreement]   BETWEEN 1 AND 5)
    );
END
GO

-- One private reflection per author per activity (idempotent upsert target; avoids duplicates).
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_ActivitySelfEvaluation_Business_Activity_Evaluator'
      AND object_id = OBJECT_ID(N'[sales].[ActivitySelfEvaluation]')
)
BEGIN
    CREATE UNIQUE INDEX [UX_ActivitySelfEvaluation_Business_Activity_Evaluator]
        ON [sales].[ActivitySelfEvaluation] ([BusinessId], [ProspectActivityId], [EvaluatorUserId]);
END
GO

-- Supports the author-scoped personal-development dashboard (Phase C) and date-range queries.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_ActivitySelfEvaluation_Business_Evaluator_Created'
      AND object_id = OBJECT_ID(N'[sales].[ActivitySelfEvaluation]')
)
BEGIN
    CREATE INDEX [IX_ActivitySelfEvaluation_Business_Evaluator_Created]
        ON [sales].[ActivitySelfEvaluation] ([BusinessId], [EvaluatorUserId], [CreatedAtUtc]);
END
GO
