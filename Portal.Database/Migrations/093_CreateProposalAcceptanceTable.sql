/*
    Migration: 093_CreateProposalAcceptanceTable
    Description: Creates the [quotation].[ProposalAcceptance] table — stores immutable
                 audit records capturing a customer's formal acceptance of a shared
                 proposal. Each record includes the acceptance terms text, UTC timestamp,
                 client IP address, and user-agent string. A UNIQUE constraint on
                 ProposalShareId enforces the one-acceptance-per-share invariant.

    Requirements: 7.1 - THE Proposal_Acceptance_System SHALL store the exact Acceptance_Terms text
                  7.2 - THE Proposal_Acceptance_System SHALL store the client IP address
                  7.3 - THE Proposal_Acceptance_System SHALL store the user-agent string
                  7.4 - THE Proposal_Acceptance_System SHALL store the UTC timestamp
                  7.5 - THE Proposal_Acceptance_System SHALL make Acceptance_Record fields immutable
                  3.1 - THE Proposal_Acceptance_System SHALL store at most one Acceptance_Record per ProposalShare

    This script is idempotent — safe to run multiple times.
*/

-- =============================================================================
-- 1. Create [quotation].[ProposalAcceptance] table
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'quotation'
      AND TABLE_NAME = 'ProposalAcceptance'
)
BEGIN
    CREATE TABLE [quotation].[ProposalAcceptance]
    (
        [Id]              INT                IDENTITY(1,1)  NOT NULL,
        [ProposalShareId] INT                               NOT NULL,
        [AcceptedTerms]   NVARCHAR(500)                     NOT NULL,
        [AcceptedAtUtc]   DATETIMEOFFSET                    NOT NULL,
        [IpAddress]       NVARCHAR(45)                      NOT NULL,
        [UserAgent]       NVARCHAR(500)                     NOT NULL,
        [CreatedAtUtc]    DATETIMEOFFSET                    NOT NULL
            CONSTRAINT [DF_ProposalAcceptance_CreatedAtUtc] DEFAULT (SYSDATETIMEOFFSET()),

        CONSTRAINT [PK_ProposalAcceptance] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_ProposalAcceptance_ProposalShare]
            FOREIGN KEY ([ProposalShareId]) REFERENCES [quotation].[ProposalShare] ([Id]),
        CONSTRAINT [UX_ProposalAcceptance_ProposalShareId]
            UNIQUE NONCLUSTERED ([ProposalShareId])
    );
END
GO
