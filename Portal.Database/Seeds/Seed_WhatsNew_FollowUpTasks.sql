-- ============================================================
-- Seed: What's New Announcement — Follow-Up Tasks
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[FeatureAnnouncements] WHERE [Title] = N'Follow-Up Tasks')
BEGIN
    INSERT INTO [dbo].[FeatureAnnouncements]
        ([Title], [Summary], [DetailHtml], [ModuleKey], [CtaLabel], [CtaUrl], [TargetPlanTier], [IsActive], [PublishedAtUtc], [ExpiresAtUtc])
    VALUES
        (N'Follow-Up Tasks',
         N'Never miss a follow-up again. Create quick reminders from any lead and see exactly who to contact today.',
         N'<p>A lightweight task system built for sales teams who need to move fast:</p><ul><li><strong>Today''s Actions Panel</strong> — opens on the Pipeline page showing overdue, today, and tomorrow tasks at a glance</li><li><strong>One-Click Creation</strong> — hit "Schedule Follow-up" from any lead, pick a preset (Tomorrow, In 3 days, Next week), done</li><li><strong>Complete & Snooze</strong> — mark done instantly or push forward when priorities shift</li><li><strong>Tasks List</strong> — full filterable view under Opportunities &rarr; Tasks with status, type, and date range filters</li><li><strong>Overdue Badge</strong> — red count badge in the navigation so nothing slips through the cracks</li><li><strong>Lead Activity</strong> — every task appears on the lead''s timeline for full context</li></ul><p>Stop using meetings for reminders. Tasks are built for speed.</p>',
         N'sales',
         N'Open Tasks',
         N'/Sales/Tasks',
         NULL,
         1,
         GETUTCDATE(),
         NULL);
    PRINT 'Inserted announcement: Follow-Up Tasks';
END
GO
