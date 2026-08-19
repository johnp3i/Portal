using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Sales;

namespace Portal.Infrastructure.Services.Sales;

/// <summary>
/// Business logic for lead request pipeline management.
/// </summary>
public interface ILeadRequestService
{
    Task<ServiceResult> CreateLeadRequestAsync(CreateLeadRequestDto request);
    Task<ServiceResult> ChangeStageAsync(int id, int leadStatusTypeId);
    Task<ServiceResult> AssignLeadAsync(int id, string userId);
    Task<ServiceResult> AssignToTeamMemberAsync(int id, int teamMemberId);
    Task<ServiceResult> UnassignTeamMemberAsync(int id);
    Task<ServiceResult> UnassignLeadAsync(int id);
    Task<ServiceResult> CancelLeadAsync(int id, string? description);
    Task<ServiceResult> ReactivateLeadAsync(int id);
    Task<List<LeadCardDto>> GetCancelledLeadsAsync();
    Task<ServiceResult> UpdateRequestDetailsAsync(int id, string? requestText);
    Task<ServiceResult> UpdateLeadDetailsAsync(int id, int? productId, int leadSourceTypeId, int? leadSourceReferenceTypeId, string? sourceUrl, string? requestText);
    Task<ServiceResult> DeactivateLeadAsync(int id);
    Task<ServiceResult> MarkAsWonAsync(int id);
    Task<ServiceResult> LinkProposalAsync(int leadRequestId, int quotationId);
    Task<ServiceResult> LinkInvoiceAsync(int leadRequestId, int invoiceId);
    Task<LeadRequestDetailDto?> GetLeadDetailAsync(int id);
    Task<List<PipelineStageGroupDto>> GetPipelineDataAsync(string? assignedToUserId, int? productId, int? teamMemberId = null);
    Task<PagedResult<LeadTableRowDto>> GetLeadsPagedAsync(LeadFilterDto filter);
    Task SuggestStageTransitionAsync(int leadRequestId, string eventType);
    Task<ServiceResult> SetPriorityAsync(int leadRequestId, int leadPriorityTypeId);
    Task<ServiceResult> ClearPriorityAsync(int leadRequestId);
    Task<List<LeadPriorityTypeDto>> GetPriorityTypesAsync();
}
