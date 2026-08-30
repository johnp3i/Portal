using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for purchase management including VAT calculation, origin type handling,
/// and bulk operations with transactional support.
/// </summary>
public class PurchaseService : IPurchaseService
{
    private readonly ICurrentTenantService _currentTenantService;
    private readonly PurchaseRepository _purchaseRepository;
    private readonly SupplierRepository _supplierRepository;
    private readonly ExpenseCategoryRepository _expenseCategoryRepository;
    private readonly AuditLogRepository _auditLogRepository;
    private readonly PortalDbContext _portalDbContext;

    public PurchaseService(
        ICurrentTenantService currentTenantService,
        PurchaseRepository purchaseRepository,
        SupplierRepository supplierRepository,
        ExpenseCategoryRepository expenseCategoryRepository,
        AuditLogRepository auditLogRepository,
        PortalDbContext portalDbContext)
    {
        _currentTenantService = currentTenantService;
        _purchaseRepository = purchaseRepository;
        _supplierRepository = supplierRepository;
        _expenseCategoryRepository = expenseCategoryRepository;
        _auditLogRepository = auditLogRepository;
        _portalDbContext = portalDbContext;
    }

    public async Task<List<Purchase>> GetPurchasesAsync()
    {
        return await _purchaseRepository.GetAllByBusinessIdAsync(_currentTenantService.CurrentBusinessId);
    }

    public async Task<List<Purchase>> GetFilteredPurchasesAsync(
        int? supplierId,
        int? expenseCategoryId,
        DateOnly? dateFrom,
        DateOnly? dateTo)
    {
        return await _purchaseRepository.GetFilteredAsync(
            _currentTenantService.CurrentBusinessId,
            supplierId,
            expenseCategoryId,
            dateFrom,
            dateTo);
    }

    public async Task<Purchase?> GetPurchaseByIdAsync(int id)
    {
        return await _purchaseRepository.GetByIdAndBusinessIdAsync(id, _currentTenantService.CurrentBusinessId);
    }

    public async Task<Purchase?> GetMostRecentBySupplierAsync(int supplierId)
    {
        return await _purchaseRepository.GetMostRecentBySupplierAsync(_currentTenantService.CurrentBusinessId, supplierId);
    }

    public async Task<ServiceResult> CreatePurchaseAsync(Purchase purchase)
    {
        var validationResult = await ValidatePurchaseAsync(purchase);
        if (!validationResult.Success)
        {
            return validationResult;
        }

        ApplyOriginTypeLogic(purchase);

        purchase.BusinessId = _currentTenantService.CurrentBusinessId;
        purchase.CreatedAtUtc = DateTime.UtcNow;
        purchase.UpdatedAtUtc = DateTime.UtcNow;

        await _purchaseRepository.InsertAsync(purchase);

        await _auditLogRepository.InsertAsync(new AuditLog
        {
            BusinessId = _currentTenantService.CurrentBusinessId,
            Action = "Create",
            TableName = "purchase.Purchase",
            RecordId = purchase.Id.ToString(),
            Timestamp = DateTime.UtcNow
        });

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> UpdatePurchaseAsync(Purchase purchase)
    {
        var existing = await _purchaseRepository.GetByIdAndBusinessIdAsync(purchase.Id, _currentTenantService.CurrentBusinessId);
        if (existing == null)
        {
            return ServiceResult.Fail("Purchase not found.");
        }

        // Locking: if existing purchase is assigned to a submitted period, reject VatSubmissionPeriodId changes
        if (existing.VatSubmissionPeriodId.HasValue && existing.VatSubmissionPeriodId != purchase.VatSubmissionPeriodId)
        {
            var isLocked = await _portalDbContext.VatSubmissions
                .AnyAsync(s => s.BusinessId == _currentTenantService.CurrentBusinessId
                    && s.VatSubmissionPeriodId == existing.VatSubmissionPeriodId
                    && s.IsSubmitted);

            if (isLocked)
            {
                return ServiceResult.Fail("Cannot change VAT period — this purchase is locked to a submitted period.");
            }
        }

        var validationResult = await ValidatePurchaseAsync(purchase);
        if (!validationResult.Success)
        {
            return validationResult;
        }

        ApplyOriginTypeLogic(purchase);

        purchase.BusinessId = _currentTenantService.CurrentBusinessId;
        purchase.UpdatedAtUtc = DateTime.UtcNow;

        await _purchaseRepository.UpdateAsync(purchase);

        await _auditLogRepository.InsertAsync(new AuditLog
        {
            BusinessId = _currentTenantService.CurrentBusinessId,
            Action = "Update",
            TableName = "purchase.Purchase",
            RecordId = purchase.Id.ToString(),
            Timestamp = DateTime.UtcNow
        });

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> CancelPurchaseAsync(int id)
    {
        var existing = await _purchaseRepository.GetByIdAndBusinessIdAsync(id, _currentTenantService.CurrentBusinessId);
        if (existing == null)
        {
            return ServiceResult.Fail("Purchase not found.");
        }

        if (existing.IsCancelled)
        {
            return ServiceResult.Fail("This purchase has already been cancelled.");
        }

        await _purchaseRepository.CancelAsync(id, _currentTenantService.CurrentBusinessId);

        await _auditLogRepository.InsertAsync(new AuditLog
        {
            BusinessId = _currentTenantService.CurrentBusinessId,
            Action = "Cancel",
            TableName = "purchase.Purchase",
            RecordId = id.ToString(),
            Timestamp = DateTime.UtcNow
        });

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> BulkCreatePurchasesAsync(List<Purchase> purchases)
    {
        var errors = new List<string>();

        for (int i = 0; i < purchases.Count; i++)
        {
            var rowNumber = i + 1;
            var purchase = purchases[i];

            var validationResult = await ValidatePurchaseAsync(purchase);
            if (!validationResult.Success)
            {
                errors.Add($"Row {rowNumber}: {validationResult.Message}");
            }
        }

        if (errors.Count > 0)
        {
            return ServiceResult.Fail($"Batch contains validation errors. No records were saved. {string.Join(" ", errors)}");
        }

        using var transaction = await _portalDbContext.Database.BeginTransactionAsync();

        try
        {
            foreach (var purchase in purchases)
            {
                ApplyOriginTypeLogic(purchase);

                purchase.BusinessId = _currentTenantService.CurrentBusinessId;
                purchase.CreatedAtUtc = DateTime.UtcNow;
                purchase.UpdatedAtUtc = DateTime.UtcNow;

                await _purchaseRepository.InsertAsync(purchase);

                await _auditLogRepository.InsertAsync(new AuditLog
                {
                    BusinessId = _currentTenantService.CurrentBusinessId,
                    Action = "Create",
                    TableName = "purchase.Purchase",
                    RecordId = purchase.Id.ToString(),
                    Timestamp = DateTime.UtcNow
                });
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> AssignPurchasesToPeriodAsync(int businessId, int periodId, List<int> purchaseIds)
    {
        try
        {
            if (purchaseIds == null || purchaseIds.Count == 0)
                return ServiceResult.Fail("No purchases selected.");

            // Validate period exists and belongs to business
            var period = await _portalDbContext.VatSubmissionPeriods
                .FirstOrDefaultAsync(p => p.Id == periodId && p.BusinessId == businessId);
            if (period == null)
                return ServiceResult.Fail("VAT period not found.");

            // Check if period is submitted
            var isSubmitted = await _portalDbContext.VatSubmissions
                .AnyAsync(s => s.VatSubmissionPeriodId == periodId && s.BusinessId == businessId && s.IsSubmitted);
            if (isSubmitted)
                return ServiceResult.Fail("Cannot assign to a submitted period.");

            // Check if any purchases are locked to a submitted period
            var lockedCount = await _portalDbContext.Purchases
                .CountAsync(p => purchaseIds.Contains(p.Id)
                    && p.BusinessId == businessId
                    && p.VatSubmissionPeriodId.HasValue
                    && _portalDbContext.VatSubmissions.Any(s =>
                        s.VatSubmissionPeriodId == p.VatSubmissionPeriodId
                        && s.BusinessId == businessId
                        && s.IsSubmitted));

            if (lockedCount > 0)
                return ServiceResult.Fail($"{lockedCount} purchase(s) are locked to a submitted period and cannot be reassigned.");

            var rowsAffected = await _purchaseRepository.BulkAssignToPeriodAsync(businessId, periodId, purchaseIds);

            // Audit log
            await _auditLogRepository.InsertAsync(new AuditLog
            {
                BusinessId = businessId,
                Action = "BulkAssignToVatPeriod",
                TableName = "purchase.Purchase",
                RecordId = $"PeriodId={periodId}, Count={rowsAffected}",
                Timestamp = DateTime.UtcNow
            });

            return ServiceResult.Ok(rowsAffected);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> UnassignPurchasesFromPeriodAsync(int businessId, List<int> purchaseIds)
    {
        try
        {
            if (purchaseIds == null || purchaseIds.Count == 0)
                return ServiceResult.Fail("No purchases selected.");

            // Check if any purchases are locked to a submitted period
            var lockedCount = await _portalDbContext.Purchases
                .CountAsync(p => purchaseIds.Contains(p.Id)
                    && p.BusinessId == businessId
                    && p.VatSubmissionPeriodId.HasValue
                    && _portalDbContext.VatSubmissions.Any(s =>
                        s.VatSubmissionPeriodId == p.VatSubmissionPeriodId
                        && s.BusinessId == businessId
                        && s.IsSubmitted));

            if (lockedCount > 0)
                return ServiceResult.Fail($"{lockedCount} purchase(s) are locked to a submitted period and cannot be unassigned.");

            var rowsAffected = await _purchaseRepository.BulkUnassignFromPeriodAsync(businessId, purchaseIds);

            // Audit log
            await _auditLogRepository.InsertAsync(new AuditLog
            {
                BusinessId = businessId,
                Action = "UnassignFromVatPeriod",
                TableName = "purchase.Purchase",
                RecordId = $"Count={rowsAffected}, PurchaseIds=[{string.Join(",", purchaseIds)}]",
                Timestamp = DateTime.UtcNow
            });

            return ServiceResult.Ok(rowsAffected);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<Purchase>> GetUnassignedForPeriodAsync(int businessId, int periodId)
    {
        try
        {
            var period = await _portalDbContext.VatSubmissionPeriods
                .FirstOrDefaultAsync(p => p.Id == periodId && p.BusinessId == businessId);

            if (period == null)
                return new List<Purchase>();

            return await _purchaseRepository.GetUnassignedByDateRangeAsync(businessId, period.PeriodStartDate, period.PeriodEndDate);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<int> CountUnassignedForPeriodAsync(int businessId, int periodId)
    {
        try
        {
            var period = await _portalDbContext.VatSubmissionPeriods
                .FirstOrDefaultAsync(p => p.Id == periodId && p.BusinessId == businessId);

            if (period == null)
                return 0;

            return await _purchaseRepository.CountUnassignedByDateRangeAsync(businessId, period.PeriodStartDate, period.PeriodEndDate);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private async Task<ServiceResult> ValidatePurchaseAsync(Purchase purchase)
    {
        if (purchase.AmountExcludingVat <= 0)
        {
            return ServiceResult.Fail("Amount excluding VAT must be greater than zero.");
        }

        if (purchase.VatAmount < 0)
        {
            return ServiceResult.Fail("VAT amount cannot be negative.");
        }

        if (purchase.PurchaseOriginTypeId < 1 || purchase.PurchaseOriginTypeId > 4)
        {
            return ServiceResult.Fail("Invalid purchase origin type.");
        }

        if (purchase.PurchaseTypeId < 1 || purchase.PurchaseTypeId > 3)
        {
            return ServiceResult.Fail("Purchase type is required. Select Asset, Stock, or Expense.");
        }

        // Validate Country requirement for EU RC, Non-EU, and EU Paid
        if (purchase.PurchaseOriginTypeId == 2 && string.IsNullOrWhiteSpace(purchase.Country))
        {
            return ServiceResult.Fail("Country is required for EU Reverse Charge transactions.");
        }

        if (purchase.PurchaseOriginTypeId == 3 && string.IsNullOrWhiteSpace(purchase.Country))
        {
            return ServiceResult.Fail("Country is required for Non-EU purchases.");
        }

        if (purchase.PurchaseOriginTypeId == 4 && string.IsNullOrWhiteSpace(purchase.Country))
        {
            return ServiceResult.Fail("Country is required for EU Paid purchases.");
        }

        // Validate SupplierId references an active supplier belonging to the current tenant
        var supplier = await _supplierRepository.GetByIdAndBusinessIdAsync(purchase.SupplierId, _currentTenantService.CurrentBusinessId);
        if (supplier == null || !supplier.IsActive)
        {
            return ServiceResult.Fail("Selected supplier is not active or does not exist.");
        }

        // Validate ExpenseCategoryId references an active expense category belonging to the current tenant
        var expenseCategory = await _expenseCategoryRepository.GetByIdAndBusinessIdAsync(purchase.ExpenseCategoryId, _currentTenantService.CurrentBusinessId);
        if (expenseCategory == null || !expenseCategory.IsActive)
        {
            return ServiceResult.Fail("Selected expense category is not active or does not exist.");
        }

        return ServiceResult.Ok();
    }

    private static void ApplyOriginTypeLogic(Purchase purchase)
    {
        switch (purchase.PurchaseOriginTypeId)
        {
            case 2: // EU Reverse Charge — force VatAmount to 0, TotalAmount = AmountExcludingVat
                purchase.VatAmount = 0;
                purchase.TotalAmount = purchase.AmountExcludingVat;
                break;

            case 1: // Domestic — preserve VatAmount, compute TotalAmount
            case 3: // Non-EU — preserve VatAmount, compute TotalAmount
            case 4: // EU Paid — preserve VatAmount, compute TotalAmount (same as Domestic/Non-EU)
            default:
                purchase.TotalAmount = purchase.AmountExcludingVat + purchase.VatAmount;
                break;
        }
    }
}
