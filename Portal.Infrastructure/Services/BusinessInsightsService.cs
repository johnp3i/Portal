using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Service layer for the Business Insights admin page.
/// Aggregates data from Portal DB (business activity) and Membership DB (owner info).
/// </summary>
public class BusinessInsightsService : IBusinessInsightsService
{
    private readonly BusinessInsightsRepository _repository;
    private readonly MembershipDbContext _membershipDb;

    public BusinessInsightsService(
        BusinessInsightsRepository repository,
        MembershipDbContext membershipDb)
    {
        _repository = repository;
        _membershipDb = membershipDb;
    }

    /// <inheritdoc />
    public async Task<(List<BusinessInsightDto> Items, BusinessInsightSummaryDto Summary, int TotalCount)> GetBusinessInsightsAsync(BusinessInsightFilter filter)
    {
        try
        {
            // 1. Get raw aggregated data from Portal DB
            var rows = await _repository.GetBusinessActivityAsync();

            // 2. Resolve owner info from Membership DB (user with IsOwner = true per business)
            var businessIds = rows.Select(r => r.BusinessId).ToList();
            var owners = await _membershipDb.UserBusinesses
                .Include(ub => ub.User)
                .Where(ub => businessIds.Contains(ub.BusinessId) && ub.IsOwner)
                .ToListAsync();

            var ownerLookup = owners.ToDictionary(
                ub => ub.BusinessId,
                ub => new
                {
                    FullName = (ub.User.FirstName + " " + ub.User.LastName).Trim(),
                    Email = ub.User.Email ?? string.Empty,
                    IsEmailConfirmed = ub.User.EmailConfirmed
                });

            // 3. Map to DTOs
            var dtos = rows.Select(row =>
            {
                var owner = ownerLookup.GetValueOrDefault(row.BusinessId);
                var lastActivity = new[] { row.LastQuotationDate, row.LastInvoiceDate, row.LastPurchaseDate }
                    .Where(d => d.HasValue)
                    .Select(d => d!.Value)
                    .DefaultIfEmpty()
                    .Max();

                return new BusinessInsightDto
                {
                    BusinessId = row.BusinessId,
                    BusinessName = row.BusinessName,
                    OwnerFullName = owner?.FullName ?? "No Owner",
                    OwnerEmail = owner?.Email ?? string.Empty,
                    IsEmailConfirmed = owner?.IsEmailConfirmed ?? false,
                    PlanName = row.PlanName,
                    Status = row.Status,
                    QuotationCount = row.QuotationCount,
                    InvoiceCount = row.InvoiceCount,
                    PurchaseCount = row.PurchaseCount,
                    RevenueTotal = row.RevenueTotal,
                    LastActivityUtc = lastActivity == default ? null : lastActivity
                };
            }).ToList();

            // 4. Build summary before filtering
            var now = DateTime.UtcNow;
            var thirtyDaysAgo = now.AddDays(-30);

            var summary = new BusinessInsightSummaryDto
            {
                TotalBusinesses = dtos.Count,
                ConfirmedAccounts = dtos.Count(d => d.IsEmailConfirmed),
                ActiveLast30Days = dtos.Count(d => d.LastActivityUtc.HasValue && d.LastActivityUtc.Value >= thirtyDaysAgo),
                OnTrial = dtos.Count(d => d.Status == "trial")
            };

            // 5. Apply filters
            var filtered = dtos.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var term = filter.SearchTerm.ToLower();
                filtered = filtered.Where(d =>
                    d.BusinessName.ToLower().Contains(term) ||
                    d.OwnerFullName.ToLower().Contains(term) ||
                    d.OwnerEmail.ToLower().Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(filter.PlanFilter))
            {
                filtered = filtered.Where(d =>
                    d.PlanName.Equals(filter.PlanFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(filter.StatusFilter))
            {
                filtered = filtered.Where(d =>
                    d.Status.Equals(filter.StatusFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(filter.ActivityFilter))
            {
                filtered = filter.ActivityFilter switch
                {
                    "Active30" => filtered.Where(d => d.LastActivityUtc.HasValue && d.LastActivityUtc.Value >= thirtyDaysAgo),
                    "Inactive30" => filtered.Where(d => d.LastActivityUtc.HasValue && d.LastActivityUtc.Value < thirtyDaysAgo),
                    "Never" => filtered.Where(d => !d.LastActivityUtc.HasValue),
                    _ => filtered
                };
            }

            // 6. Sort by last activity (most recent first), then by name
            var sorted = filtered
                .OrderByDescending(d => d.LastActivityUtc ?? DateTime.MinValue)
                .ThenBy(d => d.BusinessName)
                .ToList();

            var totalCount = sorted.Count;

            // 7. Paginate
            var pageSize = Math.Clamp(filter.PageSize, 1, 100);
            var pageNumber = Math.Max(1, filter.PageNumber);
            var skip = (pageNumber - 1) * pageSize;

            var paged = sorted.Skip(skip).Take(pageSize).ToList();

            return (paged, summary, totalCount);
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
