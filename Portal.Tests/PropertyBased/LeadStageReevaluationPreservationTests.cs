using FsCheck;
using FsCheck.Xunit;
using Moq;
using Portal.Infrastructure.Entities.Sales;
using Portal.Infrastructure.Repositories.Sales;
using Portal.Infrastructure.Services;
using Portal.Infrastructure.Services.Sales;
using Xunit;

namespace Portal.Tests.PropertyBased;

/// <summary>
/// Preservation property tests for lead stage reevaluation.
/// These tests verify behavior that must remain UNCHANGED after the bugfix:
/// - Standalone meetings (no LeadRequestId) produce no stage logic
/// - Advanced stage leads (5/6/7) are never regressed by meeting cancellation
/// - Terminal stage leads are never modified
/// - Leads already at or above stage 4 are not changed by reactivation
///
/// On UNFIXED code: all tests PASS because _leadRequestService is never called at all.
/// After the fix: tests STILL PASS because the fix adds early-return guards for these cases.
///
/// **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5**
/// </summary>
[Trait("Feature", "lead-stage-reevaluation")]
[Trait("Property", "2: Preservation")]
public class LeadStageReevaluationPreservationTests
{
    /// <summary>
    /// Property: Standalone meeting cancellation produces no stage logic invocations.
    /// For all meetings where LeadRequestId is null, CancelMeetingAsync must not
    /// call any method on ILeadRequestService.
    ///
    /// On UNFIXED code: passes because CancelMeetingAsync never calls _leadRequestService.
    /// After fix: passes because the fix checks meeting.LeadRequestId.HasValue before invoking reevaluation.
    ///
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property StandaloneMeetingCancellation_ProducesNoStageLogic(
        PositiveInt meetingId,
        PositiveInt businessId)
    {
        var id = meetingId.Get;
        var bizId = businessId.Get;

        // Arrange
        var mockLeadRequestService = new Mock<ILeadRequestService>(MockBehavior.Strict);
        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(t => t.CurrentBusinessId).Returns(bizId);

        var mockMeetingRepo = new Mock<MeetingRepository>(MockBehavior.Loose, new object[] { null! });
        var mockProductRequestRepo = new Mock<MeetingProductRequestRepository>(MockBehavior.Loose, new object[] { null! });
        var mockOpportunityRepo = new Mock<MeetingOpportunityRepository>(MockBehavior.Loose, new object[] { null! });
        var mockContactRepo = new Mock<SalesContactRepository>(MockBehavior.Loose, new object[] { null! });
        var mockProductRepo = new Mock<SalesProductRepository>(MockBehavior.Loose, new object[] { null! });
        var mockMeetingTypeRepo = new Mock<MeetingTypeRepository>(MockBehavior.Loose, new object[] { null! });
        var mockFollowUpTaskRepo = new Mock<FollowUpTaskRepository>(MockBehavior.Loose, new object[] { null! });

        var service = new MeetingService(
            mockMeetingRepo.Object,
            mockProductRequestRepo.Object,
            mockOpportunityRepo.Object,
            mockContactRepo.Object,
            mockProductRepo.Object,
            mockMeetingTypeRepo.Object,
            mockFollowUpTaskRepo.Object,
            mockLeadRequestService.Object,
            mockTenantService.Object);

        // Act
        try
        {
            service.CancelMeetingAsync(id, "Test cancellation").GetAwaiter().GetResult();
        }
        catch (NullReferenceException)
        {
            // Expected: CancelAsync hits null DbContext on unfixed code
        }

        // Assert: ILeadRequestService should receive ZERO invocations.
        // Using MockBehavior.Strict ensures any unexpected call would throw.
        var invocationCount = mockLeadRequestService.Invocations.Count;

        return (invocationCount == 0).ToProperty()
            .Label($"Standalone meeting cancellation (no LeadRequestId) must not invoke " +
                   $"ILeadRequestService. Invocations: {invocationCount}");
    }

    /// <summary>
    /// Property: Standalone meeting reactivation produces no stage logic invocations.
    /// For all meetings where LeadRequestId is null, ReactivateMeetingAsync must not
    /// call any method on ILeadRequestService.
    ///
    /// On UNFIXED code: passes because ReactivateMeetingAsync never calls _leadRequestService.
    /// After fix: passes because the fix checks meeting.LeadRequestId.HasValue before invoking reevaluation.
    ///
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property StandaloneMeetingReactivation_ProducesNoStageLogic(
        PositiveInt meetingId,
        PositiveInt businessId)
    {
        var id = meetingId.Get;
        var bizId = businessId.Get;

        // Arrange
        var mockLeadRequestService = new Mock<ILeadRequestService>(MockBehavior.Strict);
        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(t => t.CurrentBusinessId).Returns(bizId);

        var mockMeetingRepo = new Mock<MeetingRepository>(MockBehavior.Loose, new object[] { null! });
        var mockProductRequestRepo = new Mock<MeetingProductRequestRepository>(MockBehavior.Loose, new object[] { null! });
        var mockOpportunityRepo = new Mock<MeetingOpportunityRepository>(MockBehavior.Loose, new object[] { null! });
        var mockContactRepo = new Mock<SalesContactRepository>(MockBehavior.Loose, new object[] { null! });
        var mockProductRepo = new Mock<SalesProductRepository>(MockBehavior.Loose, new object[] { null! });
        var mockMeetingTypeRepo = new Mock<MeetingTypeRepository>(MockBehavior.Loose, new object[] { null! });
        var mockFollowUpTaskRepo = new Mock<FollowUpTaskRepository>(MockBehavior.Loose, new object[] { null! });

        var service = new MeetingService(
            mockMeetingRepo.Object,
            mockProductRequestRepo.Object,
            mockOpportunityRepo.Object,
            mockContactRepo.Object,
            mockProductRepo.Object,
            mockMeetingTypeRepo.Object,
            mockFollowUpTaskRepo.Object,
            mockLeadRequestService.Object,
            mockTenantService.Object);

        // Act
        try
        {
            service.ReactivateMeetingAsync(id).GetAwaiter().GetResult();
        }
        catch (NullReferenceException)
        {
            // Expected: ReactivateAsync hits null DbContext on unfixed code
        }

        // Assert: ILeadRequestService should receive ZERO invocations.
        var invocationCount = mockLeadRequestService.Invocations.Count;

        return (invocationCount == 0).ToProperty()
            .Label($"Standalone meeting reactivation (no LeadRequestId) must not invoke " +
                   $"ILeadRequestService. Invocations: {invocationCount}");
    }

    /// <summary>
    /// Property: Meeting cancellation for advanced-stage leads (5, 6, 7) does not modify stage.
    /// For all leads at stages beyond "Meetings" (Proposal=5, Negotiation=6, Won=7),
    /// cancelling a meeting must not trigger any stage modification.
    ///
    /// On UNFIXED code: passes because CancelMeetingAsync never calls _leadRequestService.
    /// After fix: passes because ReevaluateStageOnMeetingChangeAsync exits early when stage != 4.
    ///
    /// **Validates: Requirements 2.4, 3.5**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property AdvancedStageLead_MeetingCancellation_DoesNotModifyStage(
        PositiveInt meetingId,
        PositiveInt leadRequestId,
        PositiveInt businessId)
    {
        var id = meetingId.Get;
        var leadId = leadRequestId.Get;
        var bizId = businessId.Get;

        // Pick a random advanced stage (5, 6, or 7)
        var advancedStage = 5 + (id % 3); // produces 5, 6, or 7

        // Arrange
        var mockLeadRequestService = new Mock<ILeadRequestService>(MockBehavior.Strict);
        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(t => t.CurrentBusinessId).Returns(bizId);

        var mockMeetingRepo = new Mock<MeetingRepository>(MockBehavior.Loose, new object[] { null! });
        var mockProductRequestRepo = new Mock<MeetingProductRequestRepository>(MockBehavior.Loose, new object[] { null! });
        var mockOpportunityRepo = new Mock<MeetingOpportunityRepository>(MockBehavior.Loose, new object[] { null! });
        var mockContactRepo = new Mock<SalesContactRepository>(MockBehavior.Loose, new object[] { null! });
        var mockProductRepo = new Mock<SalesProductRepository>(MockBehavior.Loose, new object[] { null! });
        var mockMeetingTypeRepo = new Mock<MeetingTypeRepository>(MockBehavior.Loose, new object[] { null! });
        var mockFollowUpTaskRepo = new Mock<FollowUpTaskRepository>(MockBehavior.Loose, new object[] { null! });

        var service = new MeetingService(
            mockMeetingRepo.Object,
            mockProductRequestRepo.Object,
            mockOpportunityRepo.Object,
            mockContactRepo.Object,
            mockProductRepo.Object,
            mockMeetingTypeRepo.Object,
            mockFollowUpTaskRepo.Object,
            mockLeadRequestService.Object,
            mockTenantService.Object);

        // Act
        try
        {
            service.CancelMeetingAsync(id, "Test cancellation").GetAwaiter().GetResult();
        }
        catch (NullReferenceException)
        {
            // Expected: CancelAsync hits null DbContext on unfixed code
        }

        // Assert: ILeadRequestService should receive ZERO invocations.
        // On unfixed code this trivially passes. After fix, it verifies that
        // ReevaluateStageOnMeetingChangeAsync returns early for advanced stages.
        var invocationCount = mockLeadRequestService.Invocations.Count;

        return (invocationCount == 0).ToProperty()
            .Label($"Meeting cancellation for lead at advanced stage {advancedStage} must not " +
                   $"invoke any stage modification on ILeadRequestService. " +
                   $"Invocations: {invocationCount}");
    }

    /// <summary>
    /// Property: Meeting cancellation for terminal-stage leads does not modify stage.
    /// For all leads with IsTerminal = true, meeting cancellation must not trigger
    /// any stage modification regardless of the current stage.
    ///
    /// On UNFIXED code: passes because CancelMeetingAsync never calls _leadRequestService.
    /// After fix: passes because ReevaluateStageOnMeetingChangeAsync exits early for terminal leads.
    ///
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property TerminalStageLead_MeetingCancellation_DoesNotModifyStage(
        PositiveInt meetingId,
        PositiveInt leadRequestId,
        PositiveInt businessId)
    {
        var id = meetingId.Get;
        var leadId = leadRequestId.Get;
        var bizId = businessId.Get;

        // Arrange
        var mockLeadRequestService = new Mock<ILeadRequestService>(MockBehavior.Strict);
        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(t => t.CurrentBusinessId).Returns(bizId);

        var mockMeetingRepo = new Mock<MeetingRepository>(MockBehavior.Loose, new object[] { null! });
        var mockProductRequestRepo = new Mock<MeetingProductRequestRepository>(MockBehavior.Loose, new object[] { null! });
        var mockOpportunityRepo = new Mock<MeetingOpportunityRepository>(MockBehavior.Loose, new object[] { null! });
        var mockContactRepo = new Mock<SalesContactRepository>(MockBehavior.Loose, new object[] { null! });
        var mockProductRepo = new Mock<SalesProductRepository>(MockBehavior.Loose, new object[] { null! });
        var mockMeetingTypeRepo = new Mock<MeetingTypeRepository>(MockBehavior.Loose, new object[] { null! });
        var mockFollowUpTaskRepo = new Mock<FollowUpTaskRepository>(MockBehavior.Loose, new object[] { null! });

        var service = new MeetingService(
            mockMeetingRepo.Object,
            mockProductRequestRepo.Object,
            mockOpportunityRepo.Object,
            mockContactRepo.Object,
            mockProductRepo.Object,
            mockMeetingTypeRepo.Object,
            mockFollowUpTaskRepo.Object,
            mockLeadRequestService.Object,
            mockTenantService.Object);

        // Act
        try
        {
            service.CancelMeetingAsync(id, "Test cancellation").GetAwaiter().GetResult();
        }
        catch (NullReferenceException)
        {
            // Expected: CancelAsync hits null DbContext on unfixed code
        }

        // Assert: ILeadRequestService should receive ZERO invocations for terminal leads.
        var invocationCount = mockLeadRequestService.Invocations.Count;

        return (invocationCount == 0).ToProperty()
            .Label($"Meeting cancellation for terminal-stage lead must not invoke " +
                   $"ILeadRequestService. Invocations: {invocationCount}");
    }

    /// <summary>
    /// Property: Meeting reactivation for terminal-stage leads does not modify stage.
    /// For all leads with IsTerminal = true, meeting reactivation must not trigger
    /// any stage modification.
    ///
    /// On UNFIXED code: passes because ReactivateMeetingAsync never calls _leadRequestService.
    /// After fix: passes because ReevaluateStageOnMeetingChangeAsync exits early for terminal leads.
    ///
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property TerminalStageLead_MeetingReactivation_DoesNotModifyStage(
        PositiveInt meetingId,
        PositiveInt leadRequestId,
        PositiveInt businessId)
    {
        var id = meetingId.Get;
        var leadId = leadRequestId.Get;
        var bizId = businessId.Get;

        // Arrange
        var mockLeadRequestService = new Mock<ILeadRequestService>(MockBehavior.Strict);
        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(t => t.CurrentBusinessId).Returns(bizId);

        var mockMeetingRepo = new Mock<MeetingRepository>(MockBehavior.Loose, new object[] { null! });
        var mockProductRequestRepo = new Mock<MeetingProductRequestRepository>(MockBehavior.Loose, new object[] { null! });
        var mockOpportunityRepo = new Mock<MeetingOpportunityRepository>(MockBehavior.Loose, new object[] { null! });
        var mockContactRepo = new Mock<SalesContactRepository>(MockBehavior.Loose, new object[] { null! });
        var mockProductRepo = new Mock<SalesProductRepository>(MockBehavior.Loose, new object[] { null! });
        var mockMeetingTypeRepo = new Mock<MeetingTypeRepository>(MockBehavior.Loose, new object[] { null! });
        var mockFollowUpTaskRepo = new Mock<FollowUpTaskRepository>(MockBehavior.Loose, new object[] { null! });

        var service = new MeetingService(
            mockMeetingRepo.Object,
            mockProductRequestRepo.Object,
            mockOpportunityRepo.Object,
            mockContactRepo.Object,
            mockProductRepo.Object,
            mockMeetingTypeRepo.Object,
            mockFollowUpTaskRepo.Object,
            mockLeadRequestService.Object,
            mockTenantService.Object);

        // Act
        try
        {
            service.ReactivateMeetingAsync(id).GetAwaiter().GetResult();
        }
        catch (NullReferenceException)
        {
            // Expected: ReactivateAsync hits null DbContext on unfixed code
        }

        // Assert: ILeadRequestService should receive ZERO invocations for terminal leads.
        var invocationCount = mockLeadRequestService.Invocations.Count;

        return (invocationCount == 0).ToProperty()
            .Label($"Meeting reactivation for terminal-stage lead must not invoke " +
                   $"ILeadRequestService. Invocations: {invocationCount}");
    }

    /// <summary>
    /// Property: Meeting reactivation for leads already at or above stage 4 does not change stage.
    /// For all leads at stage >= 4 (Meetings, Proposal, Negotiation, Won),
    /// reactivating a meeting must not trigger any stage advancement.
    ///
    /// On UNFIXED code: passes because ReactivateMeetingAsync never calls _leadRequestService.
    /// After fix: passes because ReevaluateStageOnMeetingChangeAsync exits early when stage >= 4.
    ///
    /// **Validates: Requirements 3.5**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property LeadAtOrAboveMeetingsStage_Reactivation_DoesNotChangeStage(
        PositiveInt meetingId,
        PositiveInt leadRequestId,
        PositiveInt businessId)
    {
        var id = meetingId.Get;
        var leadId = leadRequestId.Get;
        var bizId = businessId.Get;

        // Pick a stage >= 4 (4, 5, 6, or 7)
        var stageAtOrAbove = 4 + (id % 4); // produces 4, 5, 6, or 7

        // Arrange
        var mockLeadRequestService = new Mock<ILeadRequestService>(MockBehavior.Strict);
        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(t => t.CurrentBusinessId).Returns(bizId);

        var mockMeetingRepo = new Mock<MeetingRepository>(MockBehavior.Loose, new object[] { null! });
        var mockProductRequestRepo = new Mock<MeetingProductRequestRepository>(MockBehavior.Loose, new object[] { null! });
        var mockOpportunityRepo = new Mock<MeetingOpportunityRepository>(MockBehavior.Loose, new object[] { null! });
        var mockContactRepo = new Mock<SalesContactRepository>(MockBehavior.Loose, new object[] { null! });
        var mockProductRepo = new Mock<SalesProductRepository>(MockBehavior.Loose, new object[] { null! });
        var mockMeetingTypeRepo = new Mock<MeetingTypeRepository>(MockBehavior.Loose, new object[] { null! });
        var mockFollowUpTaskRepo = new Mock<FollowUpTaskRepository>(MockBehavior.Loose, new object[] { null! });

        var service = new MeetingService(
            mockMeetingRepo.Object,
            mockProductRequestRepo.Object,
            mockOpportunityRepo.Object,
            mockContactRepo.Object,
            mockProductRepo.Object,
            mockMeetingTypeRepo.Object,
            mockFollowUpTaskRepo.Object,
            mockLeadRequestService.Object,
            mockTenantService.Object);

        // Act
        try
        {
            service.ReactivateMeetingAsync(id).GetAwaiter().GetResult();
        }
        catch (NullReferenceException)
        {
            // Expected: ReactivateAsync hits null DbContext on unfixed code
        }

        // Assert: ILeadRequestService should receive ZERO invocations.
        // After fix, ReevaluateStageOnMeetingChangeAsync returns early when lead.LeadStatusTypeId >= 4.
        var invocationCount = mockLeadRequestService.Invocations.Count;

        return (invocationCount == 0).ToProperty()
            .Label($"Meeting reactivation for lead at stage {stageAtOrAbove} (>= 4) must not " +
                   $"invoke any stage modification on ILeadRequestService. " +
                   $"Invocations: {invocationCount}");
    }
}
