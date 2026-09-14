-- ============================================================
-- Migration 209: Seed What's New announcements for Digital Assistants
-- ============================================================
-- Purpose: Publish "What's New" feature announcements for the shipped Digital
--          Assistants so users are educated about the new automations. Surfaces
--          in the topbar sparkle panel + dashboard banner via IAnnouncementService.
--
--          Target tier = 'Professional' — Digital Assistants is a Professional+
--          module (see Subscription_Tier_Model.md). IsTierVisible ranks
--          Starter/Foundation=1, Professional=2, Enterprise=3 and shows an
--          announcement when userRank >= targetRank, so 'Professional' correctly
--          reaches Professional and Enterprise users only.
--
--          One headline announcement introduces the capability and lists every
--          shipped assistant (a single card rather than four, to avoid flooding
--          the panel on first login). ModuleKey is a non-filtering tag.
--          Publishes immediately (GETUTCDATE()) and auto-retires after 90 days
--          (ExpiresAtUtc = GETUTCDATE() + 90d).
--
--          Idempotent: each insert is guarded by IF NOT EXISTS on [Title] (there
--          is no unique key on FeatureAnnouncements), so re-running is safe.
-- Schema: [dbo]
-- ============================================================

USE [Portal]
GO

-- ------------------------------------------------------------
-- Announcement 1: Digital Assistants — introduction (headline)
-- ------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM [dbo].[FeatureAnnouncements]
    WHERE [Title] = N'Meet your Digital Assistants'
)
BEGIN
    INSERT INTO [dbo].[FeatureAnnouncements]
        ([Title], [Summary], [DetailHtml], [ModuleKey], [CtaLabel], [CtaUrl],
         [TargetPlanTier], [IsActive], [PublishedAtUtc], [ExpiresAtUtc], [CreatedAtUtc])
    VALUES
    (
        N'Meet your Digital Assistants',
        N'Opt-in automations that keep customers informed and flag what needs your attention — each with its own activity log.',
        N'<p>Digital Assistants are named, opt-in helpers that run on your business''s behalf and record everything they do in an activity log. Turn on only the ones you want.</p>
<p>Available now:</p>
<ul>
    <li><strong>Thank-You Assistant</strong> — sends a courteous payment confirmation the moment a customer pays.</li>
    <li><strong>Daily Brief</strong> — a morning summary of what needs attention: overdue invoices, quotes awaiting a reply, and upcoming supplier payments.</li>
    <li><strong>New Payment Received alert</strong> — notifies you as soon as a payment lands.</li>
    <li><strong>VAT Period Due reminder</strong> — a heads-up before a VAT filing deadline, including the approximate net VAT as it stands today.</li>
    <li><strong>Task &amp; Meeting Reminder</strong> — emails each team member a daily agenda of their upcoming tasks and meetings.</li>
</ul>
<p>Enable and configure each assistant from the Digital Assistants page.</p>',
        N'digital_assistants',
        N'Explore assistants',
        N'/Assistants',
        N'Professional',
        1,
        GETUTCDATE(),
        DATEADD(DAY, 90, GETUTCDATE()),
        GETUTCDATE()
    );

    PRINT 'Seeded announcement: Meet your Digital Assistants.';
END
ELSE
    PRINT 'Announcement already exists: Meet your Digital Assistants (skipped).';
GO

-- Verify
DECLARE @seeded INT = (
    SELECT COUNT(*) FROM [dbo].[FeatureAnnouncements]
    WHERE [Title] = N'Meet your Digital Assistants'
);
IF @seeded >= 1
    PRINT 'Verified: Digital Assistants What''s New announcement present (' + CAST(@seeded AS NVARCHAR(10)) + ').';
ELSE
    PRINT 'WARNING: expected the Digital Assistants announcement to be present but found 0.';
GO
