using System.Globalization;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

public class PayrollPnlService : IPayrollPnlService
{
    private readonly PayrollRepository _payrollRepository;
    private readonly PurchaseRepository _purchaseRepository;

    private const int PurchaseTypeExpense = 3;
    private const int PurchaseOriginDomestic = 1;

    public PayrollPnlService(PayrollRepository payrollRepository, PurchaseRepository purchaseRepository)
    {
        _payrollRepository = payrollRepository;
        _purchaseRepository = purchaseRepository;
    }

    public async Task EnsurePayrollPnlSetupAsync(int businessId)
    {
        try
        {
            // Ensure "Payroll (Internal)" supplier exists
            var supplier = await _payrollRepository.GetPayrollSupplierAsync(businessId);
            if (supplier == null)
            {
                await _payrollRepository.InsertPayrollSupplierAsync(businessId);
            }

            // Ensure expense categories exist
            await _payrollRepository.GetOrCreateExpenseCategoryAsync(businessId, "Payroll - Salary Cost");
            await _payrollRepository.GetOrCreateExpenseCategoryAsync(businessId, "Payroll - Employer Contributions");
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> CreatePnlEntriesAsync(int periodId, int businessId)
    {
        try
        {
            await EnsurePayrollPnlSetupAsync(businessId);

            // Get supplier and category IDs
            var supplier = await _payrollRepository.GetPayrollSupplierAsync(businessId);
            if (supplier == null)
                return ServiceResult.Fail("Failed to initialise payroll expense categories.");

            var salaryCostCategoryId = await _payrollRepository.GetOrCreateExpenseCategoryAsync(businessId, "Payroll - Salary Cost");
            var contributionsCategoryId = await _payrollRepository.GetOrCreateExpenseCategoryAsync(businessId, "Payroll - Employer Contributions");

            // Get period details
            var period = await _payrollRepository.GetPeriodByIdAsync(periodId, businessId);
            if (period == null)
                return ServiceResult.Fail("Period not found.");

            // Calculate totals from payslips
            var payslips = await _payrollRepository.GetPayslipsByPeriodWithLinesAsync(periodId);
            var salaryCost = payslips.Sum(p => p.TotalEarnings);
            var employerContributions = payslips.Sum(p => p.TotalEmployerContributions);

            // Calculate invoice date (last day of period month)
            var invoiceDate = new DateOnly(period.Year, period.Month, DateTime.DaysInMonth(period.Year, period.Month));
            var monthName = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(period.Month);
            var description = $"Payroll - {monthName} {period.Year}";
            var now = DateTime.UtcNow;

            // Create Salary Cost entry
            await _purchaseRepository.InsertAsync(new Purchase
            {
                BusinessId = businessId,
                SupplierId = supplier.Id,
                ExpenseCategoryId = salaryCostCategoryId,
                PurchaseOriginTypeId = PurchaseOriginDomestic,
                PurchaseTypeId = PurchaseTypeExpense,
                InvoiceNumber = $"PAY-{period.Year}-{period.Month:00}-SAL",
                InvoiceDate = invoiceDate,
                Description = description,
                AmountExcludingVat = salaryCost,
                VatAmount = 0,
                TotalAmount = salaryCost,
                PayslipPeriodId = periodId,
                IsCancelled = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

            // Create Employer Contributions entry
            await _purchaseRepository.InsertAsync(new Purchase
            {
                BusinessId = businessId,
                SupplierId = supplier.Id,
                ExpenseCategoryId = contributionsCategoryId,
                PurchaseOriginTypeId = PurchaseOriginDomestic,
                PurchaseTypeId = PurchaseTypeExpense,
                InvoiceNumber = $"PAY-{period.Year}-{period.Month:00}-EMP",
                InvoiceDate = invoiceDate,
                Description = description,
                AmountExcludingVat = employerContributions,
                VatAmount = 0,
                TotalAmount = employerContributions,
                PayslipPeriodId = periodId,
                IsCancelled = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> AdjustPnlEntriesAsync(int periodId, int businessId, string userId)
    {
        try
        {
            // Find existing active entries for this period
            var existingEntries = await _payrollRepository.GetPayrollPurchasesByPeriodAsync(businessId, periodId);

            // Cancel existing entries with user attribution
            foreach (var entry in existingEntries)
            {
                await _purchaseRepository.CancelWithUserAsync(entry.Id, businessId, userId);
            }

            // Create new entries with recalculated totals
            return await CreatePnlEntriesAsync(periodId, businessId);
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
