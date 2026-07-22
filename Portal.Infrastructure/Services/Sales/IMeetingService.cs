using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Sales;

namespace Portal.Infrastructure.Services.Sales;

/// <summary>
/// Business logic for meeting management.
/// </summary>
public interface IMeetingService
{
    Task<ServiceResult> CreateMeetingAsync(CreateMeetingRequest request, string userId);
    Task<ServiceResult> UpdateMeetingAsync(UpdateMeetingRequest request);
    Task<ServiceResult> CancelMeetingAsync(int id, string? description);
    Task<MeetingDetailDto?> GetByIdAsync(int id);
    Task<List<MeetingListDto>> GetMeetingsForLeadAsync(int leadRequestId);
    Task<List<MeetingListDto>> GetAllMeetingsAsync();
    Task<byte[]> GenerateIcsFileAsync(int id);
    Task<ServiceResult> CreateProductRequestAsync(CreateMeetingProductRequestDto request);
    Task<ServiceResult> CreateOpportunityAsync(CreateMeetingOpportunityDto request);
}
