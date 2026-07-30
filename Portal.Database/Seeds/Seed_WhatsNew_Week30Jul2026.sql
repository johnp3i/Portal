-- ============================================================
-- Seed: What's New Announcements — Week of 28 Jul 2026
-- ============================================================
-- Inserts initial announcements for features released this week.
-- Idempotent — skips if an announcement with the same title already exists.
-- ============================================================

USE [Portal]
GO

-- 1. Global Search
IF NOT EXISTS (SELECT 1 FROM [dbo].[FeatureAnnouncements] WHERE [Title] = N'Global Search')
BEGIN
    INSERT INTO [dbo].[FeatureAnnouncements]
        ([Title], [Summary], [DetailHtml], [ModuleKey], [CtaLabel], [CtaUrl], [TargetPlanTier], [IsActive], [PublishedAtUtc], [ExpiresAtUtc])
    VALUES
        (N'Global Search',
         N'Find anything instantly — search across invoices, quotations, customers, purchases, and more from the top bar.',
         N'<p>Press <strong>Ctrl+K</strong> or click the search bar in the top right to search across your entire workspace.</p><p>Results are grouped by type (Invoices, Quotations, Customers, Purchases, Suppliers) and show the most relevant matches first. Click a result to navigate directly.</p>',
         NULL,
         N'Try it now',
         N'/Dashboard',
         NULL,
         1,
         '2026-07-29 15:56:00',
         NULL);
    PRINT 'Inserted announcement: Global Search';
END
GO

-- 2. Dashboard Onboarding
IF NOT EXISTS (SELECT 1 FROM [dbo].[FeatureAnnouncements] WHERE [Title] = N'Dashboard Onboarding Guide')
BEGIN
    INSERT INTO [dbo].[FeatureAnnouncements]
        ([Title], [Summary], [DetailHtml], [ModuleKey], [CtaLabel], [CtaUrl], [TargetPlanTier], [IsActive], [PublishedAtUtc], [ExpiresAtUtc])
    VALUES
        (N'Dashboard Onboarding Guide',
         N'New users now see a step-by-step onboarding checklist on the Dashboard to get started quickly.',
         N'<p>The onboarding guide walks new users through setting up their business profile, creating their first customer, quotation, and invoice. Each step links directly to the relevant page.</p><p>The checklist disappears once all steps are completed or the user dismisses it.</p>',
         NULL,
         NULL,
         NULL,
         NULL,
         1,
         '2026-07-29 16:14:00',
         NULL);
    PRINT 'Inserted announcement: Dashboard Onboarding Guide';
END
GO

-- 3. Business Applications Tracker
IF NOT EXISTS (SELECT 1 FROM [dbo].[FeatureAnnouncements] WHERE [Title] = N'Business Applications Tracker')
BEGIN
    INSERT INTO [dbo].[FeatureAnnouncements]
        ([Title], [Summary], [DetailHtml], [ModuleKey], [CtaLabel], [CtaUrl], [TargetPlanTier], [IsActive], [PublishedAtUtc], [ExpiresAtUtc])
    VALUES
        (N'Business Applications Tracker',
         N'Track permits, licences, and regulatory applications with status updates, deadlines, and document attachments.',
         N'<p>The new <strong>Compliance</strong> module lets you manage all your business applications in one place — trade licences, permits, insurance renewals, and regulatory filings.</p><p>Each application tracks its status, submission date, expiry, and attached documents. Never miss a renewal deadline again.</p>',
         N'compliance',
         N'View Applications',
         N'/Compliance',
         N'Professional',
         1,
         '2026-07-29 21:50:00',
         NULL);
    PRINT 'Inserted announcement: Business Applications Tracker';
END
GO

-- 4. Future-Dated Payments
IF NOT EXISTS (SELECT 1 FROM [dbo].[FeatureAnnouncements] WHERE [Title] = N'Future-Dated Payment Handling')
BEGIN
    INSERT INTO [dbo].[FeatureAnnouncements]
        ([Title], [Summary], [DetailHtml], [ModuleKey], [CtaLabel], [CtaUrl], [TargetPlanTier], [IsActive], [PublishedAtUtc], [ExpiresAtUtc])
    VALUES
        (N'Future-Dated Payment Handling',
         N'Cheque payments with a future date are now correctly excluded from paid totals until their date arrives.',
         N'<p>When you record a payment with a future date (common with post-dated cheques), it no longer inflates your "Paid" totals or prematurely changes invoice financial statuses.</p><p>These payments are marked with an <strong>Upcoming</strong> badge and will automatically count toward the balance once their date arrives.</p>',
         N'revenue',
         N'View Revenue',
         N'/Revenue/Dashboard',
         NULL,
         1,
         '2026-07-30 10:24:00',
         NULL);
    PRINT 'Inserted announcement: Future-Dated Payment Handling';
END
GO

-- 5. PDF Signature Fix
IF NOT EXISTS (SELECT 1 FROM [dbo].[FeatureAnnouncements] WHERE [Title] = N'PDF Signature Display Fix')
BEGIN
    INSERT INTO [dbo].[FeatureAnnouncements]
        ([Title], [Summary], [DetailHtml], [ModuleKey], [CtaLabel], [CtaUrl], [TargetPlanTier], [IsActive], [PublishedAtUtc], [ExpiresAtUtc])
    VALUES
        (N'PDF Signature Display Fix',
         N'Signatures now render correctly on exported PDF documents — no more missing images.',
         N'<p>We resolved an issue where signature images were not appearing on exported PDF invoices and quotations. Signatures captured via the client portal or manually uploaded now display correctly in all generated PDFs.</p>',
         NULL,
         NULL,
         NULL,
         NULL,
         1,
         '2026-07-29 13:34:00',
         NULL);
    PRINT 'Inserted announcement: PDF Signature Display Fix';
END
GO

-- 6. What's New Announcements (this feature itself)
IF NOT EXISTS (SELECT 1 FROM [dbo].[FeatureAnnouncements] WHERE [Title] = N'What''s New Notifications')
BEGIN
    INSERT INTO [dbo].[FeatureAnnouncements]
        ([Title], [Summary], [DetailHtml], [ModuleKey], [CtaLabel], [CtaUrl], [TargetPlanTier], [IsActive], [PublishedAtUtc], [ExpiresAtUtc])
    VALUES
        (N'What''s New Notifications',
         N'Stay up to date with the latest features — look for the sparkle icon in the top bar.',
         N'<p>You''re looking at it! The <strong>What''s New</strong> panel keeps you informed about newly released features, improvements, and fixes.</p><p>Click the sparkle icon anytime to browse announcements. Dismiss them once you''ve read them — the badge shows your unread count.</p>',
         NULL,
         NULL,
         NULL,
         NULL,
         1,
         '2026-07-30 12:46:00',
         NULL);
    PRINT 'Inserted announcement: What''s New Notifications';
END
GO
