-- ============================================================
-- Seed: What's New Announcement — Product Detail & Insights
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[FeatureAnnouncements] WHERE [Title] = N'Product Detail & Insights')
BEGIN
    INSERT INTO [dbo].[FeatureAnnouncements]
        ([Title], [Summary], [DetailHtml], [ModuleKey], [CtaLabel], [CtaUrl], [TargetPlanTier], [IsActive], [PublishedAtUtc], [ExpiresAtUtc])
    VALUES
        (N'Product Detail & Insights',
         N'Each product now has a dedicated page with sales performance, customer insights, trend charts, price history, and demand forecasting.',
         N'<p>Click any product in the <strong>Catalogue</strong> to open its Detail page. You''ll see:</p><ul><li><strong>Sales KPIs</strong> — total revenue, units sold, average price, gross margin</li><li><strong>Top Customers</strong> — who buys this product most, with repeat purchase rate</li><li><strong>Monthly Trend</strong> — 12-month revenue chart showing seasonality</li><li><strong>Price History</strong> — full timeline of every price change</li><li><strong>Demand Forecast</strong> (Professional) — projected sales for the next 30/60/90 days</li><li><strong>Pipeline Activity</strong> — active sales leads referencing this product</li></ul><p>Turn your catalogue into a revenue intelligence tool.</p>',
         N'products',
         N'View a Product',
         N'/Product',
         NULL,
         1,
         GETUTCDATE(),
         NULL);
    PRINT 'Inserted announcement: Product Detail & Insights';
END
GO
