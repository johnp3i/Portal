-- ============================================================
-- Seed default Expense Category Templates
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (SELECT 1 FROM [purchase].[ExpenseCategoryTemplate])
BEGIN
    INSERT INTO [purchase].[ExpenseCategoryTemplate] ([Name], [Description]) VALUES
        (N'Office Supplies', N'Stationery, printer ink, paper, pens, and other consumable office items'),
        (N'Utilities', N'Electricity, water, gas, and heating costs'),
        (N'Rent & Property', N'Office rent, warehouse rent, property maintenance, and rates'),
        (N'Professional Services', N'Accounting, consulting, design, and other outsourced professional fees'),
        (N'Travel & Transport', N'Business travel, fuel, parking, taxis, flights, and accommodation'),
        (N'Software & Subscriptions', N'SaaS tools, cloud services, domain renewals, and digital subscriptions'),
        (N'Insurance', N'Business insurance, liability insurance, property insurance'),
        (N'Marketing & Advertising', N'Digital ads, print advertising, sponsorships, and promotional materials'),
        (N'Bank Fees & Charges', N'Bank maintenance fees, transfer charges, card processing fees'),
        (N'Equipment & Maintenance', N'Hardware purchases, repairs, servicing, and equipment leasing'),
        (N'Telecommunications', N'Phone bills, internet, mobile plans, and communication tools'),
        (N'Training & Development', N'Staff training courses, certifications, conferences, and learning materials'),
        (N'Staff Expenses', N'Reimbursable employee expenses, meals, and entertainment'),
        (N'Cleaning & Hygiene', N'Office cleaning services, hygiene supplies, and waste disposal'),
        (N'Legal & Compliance', N'Legal fees, regulatory filings, licenses, and compliance costs');
END
GO
