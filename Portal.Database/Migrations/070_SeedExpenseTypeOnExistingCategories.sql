/*
    Migration: 070_SeedExpenseTypeOnExistingCategories
    Description: Assigns ExpenseTypeId to all existing expense categories that have NULL.
                 - Services (Id=1): AI Software, Telephone/Internet Bills, Software Subscriptions/Service Subscriptions
                 - Goods (Id=2): All other categories

    This script is idempotent — only updates rows where ExpenseTypeId IS NULL.
*/

-- Set Services (Id=1) for known service categories
UPDATE [purchase].[ExpenseCategory]
SET [ExpenseTypeId] = 1
WHERE [ExpenseTypeId] IS NULL
  AND [Name] IN (
      'AI Software',
      'Telephone/Internet Bills',
      'Software Subscriptions/Service Subscriptions'
  );
GO

-- Set Goods (Id=2) for all remaining categories without an ExpenseTypeId
UPDATE [purchase].[ExpenseCategory]
SET [ExpenseTypeId] = 2
WHERE [ExpenseTypeId] IS NULL;
GO
