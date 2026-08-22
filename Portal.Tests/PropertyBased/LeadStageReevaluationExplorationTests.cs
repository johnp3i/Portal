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
/// Bug condition exploration tests for lead stage reevaluation.
/// These tests verify the FIXED behavior:
/// - CancelMeetingAsync now loads the meeting first (via GetByIdAsync) before cancellation
/// - ReactivateMeetingAsync now loads the meeting first (via GetByIdAsync) before reactivation
///
/// On FIXED code: Both methods call GetByIdAsync first. Since the repository uses a null DbContext,
/// the NullReferenceException stack trace contains "GetByIdAsync" — proving the method now checks
/// the meeting existence before proceeding.
///
/// On UNFIXED code: Both methods went straight to CancelAsync/ReactivateAsync without loading
/// the meeting, so the stack trace would contain "CancelAsync" or "ReactivateAsync" directly.
///
/// **Validates: Requirements 1.1, 1.3, 2.1, 2.3**
/// </summary>
[Trait("Feature", "lead-stage-reevaluation")]
[Trait("Property", "1: Bug Condition")]
public class LeadStageReevaluationExplorationTests
{
    /// <summary>
    /// Property 1: Bug Condition — CancelMeetingAsync now loads the meeting first.
    ///
    /// On FIXED code: CancelMeetingAsync calls _meetingRepository.GetByIdAsync(id, businessId)
    /// first. The null DbContext causes NullReferenceException with "GetByIdAsync" in the stack.
    /// This proves the code now checks meeting existence before cancellation.
    ///
    /// On UNFIXED code: CancelMeetingAsync went straight to _meetingRepository.CancelAsync
    /// so the stack trace would show "CancelAsync" instead.
    ///
    /// **Validates: Requirements 1.1, 2.1**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property CancelMeetingAsync_ShouldLoadMeetingFirst_ProvingFixApplied(
        PositiveInt meetingId,
        PositiveInt businessId)
    {
        var id = meetingId.Get;
        var bizId = businessId.Get;

        // Arrange: create mocks
        var mockLeadRequestService = new Mock<ILeadRequestService>(MockBehavior.Loose);
        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(t => t.CurrentBusinessId).Returns(bizId);

        var mockMeetingRepo = new Mock<MeetingRepository>(MockBehavior.Loose, new object[] { null! });
        var mockProductRequestRepo = new Mock<MeetingProductRequestRepository>(MockBehavior.Loose, new object[] { null! });
        var mockOpportunityRepo = new Mock<MeetingOpportunityRepository>(MockBehavior.Loose, new object[] { null! });
        var mockContactRepo = new Mock<SalesContactRepository>(MockBehavior.Loose, new object[] { null! });
        var mockProductRepo = new Mock<SalesProductRepository>(MockBehavior.Loose, new object[] { null! });
        var mockMeetingTypeRepo = new Mock<MeetingTypeRepository>(MockBehavior.Loose, new object[] { null! });

        var service = new MeetingService(
            mockMeetingRepo.Object,
            mockProductRequestRepo.Object,
            mockOpportunityRepo.Object,
            mockContactRepo.Object,
            mockProductRepo.Object,
            mockMeetingTypeRepo.Object,
            mockLeadRequestService.Object,
            mockTenantService.Object);

        // Act: The fixed code calls GetByIdAsync first, which will throw because
        // the null DbContext can't execute raw SQL. We verify the exception origin.
        bool fixConfirmed = false;
        try
        {
            service.CancelMeetingAsync(id, "Test cancellation").GetAwaiter().GetResult();
            // If we somehow get a result (unlikely with null context), check it
            fixConfirmed = true;
        }
        catch (NullReferenceException ex)
        {
            // On FIXED code: exception originates from GetByIdAsync (loading the meeting first)
            // On UNFIXED code: exception originates from CancelAsync (direct repo call)
            var stackTrace = ex.StackTrace ?? string.Empty;
            fixConfirmed = stackTrace.Contains("GetByIdAsync");
        }
        catch (Exception)
        {
            // Any other exception - check if method structure is fixed
            fixConfirmed = false;
        }

        return fixConfirmed.ToProperty()
            .Label($"FIXED: CancelMeetingAsync now calls GetByIdAsync first (loading the meeting " +
                   $"to check LeadRequestId) before calling CancelAsync. " +
                   $"(meetingId={id}, businessId={bizId})");
    }

    /// <summary>
    /// Property 2: Bug Condition — ReactivateMeetingAsync now loads the meeting first.
    ///
    /// On FIXED code: ReactivateMeetingAsync calls _meetingRepository.GetByIdAsync(id, businessId)
    /// first. The null DbContext causes NullReferenceException with "GetByIdAsync" in the stack.
    /// This proves the code now checks meeting existence before reactivation.
    ///
    /// On UNFIXED code: ReactivateMeetingAsync went straight to _meetingRepository.ReactivateAsync
    /// so the stack trace would show "ReactivateAsync" instead.
    ///
    /// **Validates: Requirements 1.3, 2.3**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property ReactivateMeetingAsync_ShouldLoadMeetingFirst_ProvingFixApplied(
        PositiveInt meetingId,
        PositiveInt businessId)
    {
        var id = meetingId.Get;
        var bizId = businessId.Get;

        // Arrange: create mocks
        var mockLeadRequestService = new Mock<ILeadRequestService>(MockBehavior.Loose);
        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(t => t.CurrentBusinessId).Returns(bizId);

        var mockMeetingRepo = new Mock<MeetingRepository>(MockBehavior.Loose, new object[] { null! });
        var mockProductRequestRepo = new Mock<MeetingProductRequestRepository>(MockBehavior.Loose, new object[] { null! });
        var mockOpportunityRepo = new Mock<MeetingOpportunityRepository>(MockBehavior.Loose, new object[] { null! });
        var mockContactRepo = new Mock<SalesContactRepository>(MockBehavior.Loose, new object[] { null! });
        var mockProductRepo = new Mock<SalesProductRepository>(MockBehavior.Loose, new object[] { null! });
        var mockMeetingTypeRepo = new Mock<MeetingTypeRepository>(MockBehavior.Loose, new object[] { null! });

        var service = new MeetingService(
            mockMeetingRepo.Object,
            mockProductRequestRepo.Object,
            mockOpportunityRepo.Object,
            mockContactRepo.Object,
            mockProductRepo.Object,
            mockMeetingTypeRepo.Object,
            mockLeadRequestService.Object,
            mockTenantService.Object);

        // Act: The fixed code calls GetByIdAsync first, which will throw because
        // the null DbContext can't execute raw SQL. We verify the exception origin.
        bool fixConfirmed = false;
        try
        {
            service.ReactivateMeetingAsync(id).GetAwaiter().GetResult();
            // If we somehow get a result (unlikely with null context), check it
            fixConfirmed = true;
        }
        catch (NullReferenceException ex)
        {
            // On FIXED code: exception originates from GetByIdAsync (loading the meeting first)
            // On UNFIXED code: exception originates from ReactivateAsync (direct repo call)
            var stackTrace = ex.StackTrace ?? string.Empty;
            fixConfirmed = stackTrace.Contains("GetByIdAsync");
        }
        catch (Exception)
        {
            // Any other exception - check if method structure is fixed
            fixConfirmed = false;
        }

        return fixConfirmed.ToProperty()
            .Label($"FIXED: ReactivateMeetingAsync now calls GetByIdAsync first (loading the meeting " +
                   $"to check LeadRequestId) before calling ReactivateAsync. " +
                   $"(meetingId={id}, businessId={bizId})");
    }
}
