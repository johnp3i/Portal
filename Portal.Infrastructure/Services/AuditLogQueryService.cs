using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Provides filtered, paginated access to the audit log for the current tenant.
/// All queries are scoped to ICurrentTenantService.CurrentBusinessId.
/// PageSize is clamped to [1, 100]; PageNumber is clamped to a minimum of 1.
/// </summary>
public class AuditLogQueryService : IAuditLogQueryService
{
    private readonly AuditLogQueryRepository _repository;
    private readonly ICurrentTenantService _tenantService;

    public AuditLogQueryService(
        AuditLogQueryRepository repository,
        ICurrentTenantService tenantService)
    {
        _repository = repository;
        _tenantService = tenantService;
    }

    /// <inheritdoc />
    public async Task<PagedResult<AuditLog>> GetAuditLogsAsync(AuditLogFilter filter)
    {
        try
        {
            // Clamp pagination parameters
            var pageSize = Math.Clamp(filter.PageSize, 1, 100);
            var pageNumber = Math.Max(filter.PageNumber, 1);

            var businessId = _tenantService.CurrentBusinessId;
            var skip = (pageNumber - 1) * pageSize;

            // Repository returns TotalCount for the full filtered set regardless of skip/take
            var (items, totalCount) = await _repository.GetPagedAsync(
                businessId,
                filter.TableName,
                filter.Action,
                filter.UserId,
                filter.DateFrom,
                filter.DateTo,
                skip,
                pageSize);

            var totalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)pageSize);

            // When PageNumber exceeds TotalPages, Items will already be empty (skip beyond data).
            // Ensure Items is explicitly empty and metadata remains accurate.
            if (pageNumber > totalPages && totalCount > 0)
            {
                return new PagedResult<AuditLog>
                {
                    Items = new List<AuditLog>(),
                    CurrentPage = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };
            }

            return new PagedResult<AuditLog>
            {
                Items = items,
                CurrentPage = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<string>> GetDistinctTableNamesAsync()
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            return await _repository.GetDistinctTableNamesAsync(businessId);
        }
        catch (Exception)
        {
            throw;
        }
    }
}
