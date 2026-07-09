/*
    Migration: 105_AddAuditLogToProfessionalPlan
    Description: Adds audit_log module to the Professional plan's PlanFeature records.
                 Ensures Professional-plan users can access the Activity Log.
    Requirements: 2.4
*/

USE [Portal]
GO

DECLARE @ProfessionalPlanId INT;
SELECT @ProfessionalPlanId = [Id] FROM [dbo].[Plan] WHERE [Slug] = 'professional';

IF @ProfessionalPlanId IS NOT NULL
AND NOT EXISTS (
    SELECT 1 FROM [dbo].[PlanFeature]
    WHERE [PlanId] = @ProfessionalPlanId AND [ModuleName] = N'audit_log'
)
BEGIN
    INSERT INTO [dbo].[PlanFeature] ([PlanId], [ModuleName], [IsIncluded], [AccessLevel], [CreatedAtUtc])
    VALUES (@ProfessionalPlanId, N'audit_log', 1, N'full', GETUTCDATE());
END
GO
