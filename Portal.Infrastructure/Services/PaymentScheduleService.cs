using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Orchestrates all payment schedule operations including creation, modification,
/// deletion, payment matching, and status computation.
/// </summary>
public interface IPaymentScheduleService
{
    Task<ServiceResult> CreateScheduleAsync(CreatePaymentScheduleDto dto, int businessId, string userId);
    Task<ServiceResult> UpdateInstalmentAsync(UpdateInstalmentDto dto, int businessId, string userId);
    Task<ServiceResult> AddInstalmentAsync(AddInstalmentDto dto, int businessId, string userId);
    Task<ServiceResult> RemoveInstalmentAsync(int instalmentId, int businessId, string userId);
    Task<ServiceResult> DeleteScheduleAsync(int scheduleId, int businessId, string userId);
    Task<PaymentScheduleDetailDto?> GetScheduleByInvoiceIdAsync(int invoiceId, int businessId);
    Task<List<PaymentScheduleHistoryDto>> GetScheduleHistoryAsync(int scheduleId, int businessId);
    Task<VatWarningDto?> GetVatWarningAsync(int invoiceId, DateOnly? firstInstalmentDueDate, decimal firstInstalmentAmount, int businessId);
    Task MatchPaymentToScheduleAsync(int paymentId, decimal paymentAmount, int invoiceId, int businessId, string userId);
    Task RevertPaymentMatchAsync(int paymentId, int invoiceId, int businessId);
}

/// <summary>
/// Implementation of IPaymentScheduleService that ties together repositories,
/// computation engines, and the VatWarningService into the full business logic layer.
/// </summary>
public class PaymentScheduleService : IPaymentScheduleService
{
    private readonly PaymentScheduleRepository _scheduleRepo;
    private readonly PaymentScheduleInstalmentRepository _instalmentRepo;
    private readonly PaymentScheduleHistoryRepository _historyRepo;
    private readonly PaymentRepository _paymentRepo;
    private readonly IInstalmentStatusEngine _statusEngine;
    private readonly IInstalmentMatchingEngine _matchingEngine;
    private readonly IVatWarningService _vatWarningService;
    private readonly IFinancialStatusEngine _financialStatusEngine;
    private readonly PortalDbContext _dbContext;

    // Status type IDs matching [revenue].[PaymentScheduleInstalmentStatusType]
    private const int StatusPending = 1;
    private const int StatusDue = 2;
    private const int StatusOverdue = 3;
    private const int StatusPaid = 4;
    private const int StatusPartiallyPaid = 5;

    private static readonly Dictionary<int, string> StatusNames = new()
    {
        { StatusPending, "Pending" },
        { StatusDue, "Due" },
        { StatusOverdue, "Overdue" },
        { StatusPaid, "Paid" },
        { StatusPartiallyPaid, "Partially Paid" }
    };

    public PaymentScheduleService(
        PaymentScheduleRepository scheduleRepo,
        PaymentScheduleInstalmentRepository instalmentRepo,
        PaymentScheduleHistoryRepository historyRepo,
        PaymentRepository paymentRepo,
        IInstalmentStatusEngine statusEngine,
        IInstalmentMatchingEngine matchingEngine,
        IVatWarningService vatWarningService,
        IFinancialStatusEngine financialStatusEngine,
        PortalDbContext dbContext)
    {
        _scheduleRepo = scheduleRepo;
        _instalmentRepo = instalmentRepo;
        _historyRepo = historyRepo;
        _paymentRepo = paymentRepo;
        _statusEngine = statusEngine;
        _matchingEngine = matchingEngine;
        _vatWarningService = vatWarningService;
        _financialStatusEngine = financialStatusEngine;
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<ServiceResult> CreateScheduleAsync(CreatePaymentScheduleDto dto, int businessId, string userId)
    {
        try
        {
            // Get the invoice to determine TotalAmount
            var invoice = await _dbContext.Invoices
                .Where(i => i.Id == dto.InvoiceId && i.BusinessId == businessId && !i.IsDeleted)
                .Select(i => new { i.TotalAmount })
                .FirstOrDefaultAsync();

            if (invoice == null)
                return ServiceResult.Fail("Invoice not found.");

            // Get total paid to calculate outstanding balance
            var totalPaid = await _paymentRepo.GetTotalPaidAsync(dto.InvoiceId, businessId);
            var outstandingBalance = invoice.TotalAmount - totalPaid;

            // Validate sum of instalments equals outstanding balance
            var instalmentSum = dto.Instalments.Sum(i => i.Amount);
            if (instalmentSum != outstandingBalance)
                return ServiceResult.Fail($"The sum of instalment amounts ({instalmentSum:N2}) does not equal the outstanding balance ({outstandingBalance:N2}).");

            // Check no existing active schedule
            var existingSchedule = await _scheduleRepo.GetByInvoiceIdAsync(dto.InvoiceId, businessId);
            if (existingSchedule != null)
                return ServiceResult.Fail("An active payment schedule already exists for this invoice.");

            // Create the schedule
            var schedule = new PaymentSchedule
            {
                BusinessId = businessId,
                InvoiceId = dto.InvoiceId,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = userId
            };

            var scheduleId = await _scheduleRepo.InsertAsync(schedule);

            // Insert all instalments in sequence
            for (int i = 0; i < dto.Instalments.Count; i++)
            {
                var instalment = new PaymentScheduleInstalment
                {
                    PaymentScheduleId = scheduleId,
                    SequenceNumber = i + 1,
                    Amount = dto.Instalments[i].Amount,
                    MatchedAmount = 0,
                    DueDate = dto.Instalments[i].DueDate,
                    PaymentId = null,
                    ParentInstalmentId = null,
                    IsRemainder = false,
                    CreatedAtUtc = DateTime.UtcNow
                };

                await _instalmentRepo.InsertAsync(instalment);
            }

            // Record "ScheduleCreated" history entry
            await _historyRepo.InsertAsync(new PaymentScheduleHistory
            {
                PaymentScheduleId = scheduleId,
                FieldChanged = "ScheduleCreated",
                OldValue = null,
                NewValue = $"{dto.Instalments.Count} instalments, total {instalmentSum:N2}",
                ChangedByUserId = userId,
                ChangedAtUtc = DateTime.UtcNow
            });

            return ServiceResult.Ok(scheduleId);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UpdateInstalmentAsync(UpdateInstalmentDto dto, int businessId, string userId)
    {
        try
        {
            // Get the instalment
            var instalment = await _instalmentRepo.GetByIdAsync(dto.InstalmentId);
            if (instalment == null)
                return ServiceResult.Fail("Instalment not found.");

            // Verify the schedule belongs to this business
            var schedule = await _scheduleRepo.GetByIdAndBusinessIdAsync(dto.ScheduleId, businessId);
            if (schedule == null)
                return ServiceResult.Fail("Payment schedule not found.");

            // Verify not Paid (MatchedAmount >= Amount)
            if (instalment.MatchedAmount >= instalment.Amount)
                return ServiceResult.Fail("Cannot modify a fully paid instalment.");

            // Update amount if requested
            if (dto.NewAmount.HasValue && dto.NewAmount.Value != instalment.Amount)
            {
                var oldAmount = instalment.Amount;
                await _instalmentRepo.UpdateAmountAsync(dto.InstalmentId, dto.NewAmount.Value);

                await _historyRepo.InsertAsync(new PaymentScheduleHistory
                {
                    PaymentScheduleId = dto.ScheduleId,
                    FieldChanged = $"Instalment #{instalment.SequenceNumber} Amount",
                    OldValue = oldAmount.ToString("N2"),
                    NewValue = dto.NewAmount.Value.ToString("N2"),
                    ChangedByUserId = userId,
                    ChangedAtUtc = DateTime.UtcNow
                });
            }

            // Update due date if requested
            if (dto.ClearDueDate)
            {
                if (instalment.DueDate != null)
                {
                    var oldDate = instalment.DueDate.Value.ToString("yyyy-MM-dd");
                    await _instalmentRepo.UpdateDueDateAsync(dto.InstalmentId, null);

                    await _historyRepo.InsertAsync(new PaymentScheduleHistory
                    {
                        PaymentScheduleId = dto.ScheduleId,
                        FieldChanged = $"Instalment #{instalment.SequenceNumber} DueDate",
                        OldValue = oldDate,
                        NewValue = null,
                        ChangedByUserId = userId,
                        ChangedAtUtc = DateTime.UtcNow
                    });
                }
            }
            else if (dto.NewDueDate.HasValue && dto.NewDueDate != instalment.DueDate)
            {
                var oldDate = instalment.DueDate?.ToString("yyyy-MM-dd");
                await _instalmentRepo.UpdateDueDateAsync(dto.InstalmentId, dto.NewDueDate.Value);

                await _historyRepo.InsertAsync(new PaymentScheduleHistory
                {
                    PaymentScheduleId = dto.ScheduleId,
                    FieldChanged = $"Instalment #{instalment.SequenceNumber} DueDate",
                    OldValue = oldDate,
                    NewValue = dto.NewDueDate.Value.ToString("yyyy-MM-dd"),
                    ChangedByUserId = userId,
                    ChangedAtUtc = DateTime.UtcNow
                });
            }

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> AddInstalmentAsync(AddInstalmentDto dto, int businessId, string userId)
    {
        try
        {
            // Verify the schedule belongs to this business
            var schedule = await _scheduleRepo.GetByIdAndBusinessIdAsync(dto.ScheduleId, businessId);
            if (schedule == null)
                return ServiceResult.Fail("Payment schedule not found.");

            // Get existing instalments to determine next sequence number
            var existingInstalments = await _instalmentRepo.GetByScheduleIdAsync(dto.ScheduleId);
            var nextSequence = existingInstalments.Count > 0
                ? existingInstalments.Max(i => i.SequenceNumber) + 1
                : 1;

            // Insert new instalment
            var instalment = new PaymentScheduleInstalment
            {
                PaymentScheduleId = dto.ScheduleId,
                SequenceNumber = nextSequence,
                Amount = dto.Amount,
                MatchedAmount = 0,
                DueDate = dto.DueDate,
                PaymentId = null,
                ParentInstalmentId = null,
                IsRemainder = false,
                CreatedAtUtc = DateTime.UtcNow
            };

            var instalmentId = await _instalmentRepo.InsertAsync(instalment);

            // Record history
            await _historyRepo.InsertAsync(new PaymentScheduleHistory
            {
                PaymentScheduleId = dto.ScheduleId,
                FieldChanged = "InstalmentAdded",
                OldValue = null,
                NewValue = $"#{nextSequence}: {dto.Amount:N2}" + (dto.DueDate.HasValue ? $" due {dto.DueDate.Value:yyyy-MM-dd}" : ""),
                ChangedByUserId = userId,
                ChangedAtUtc = DateTime.UtcNow
            });

            return ServiceResult.Ok(instalmentId);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> RemoveInstalmentAsync(int instalmentId, int businessId, string userId)
    {
        try
        {
            // Get the instalment
            var instalment = await _instalmentRepo.GetByIdAsync(instalmentId);
            if (instalment == null)
                return ServiceResult.Fail("Instalment not found.");

            // Verify the schedule belongs to this business
            var schedule = await _scheduleRepo.GetByIdAndBusinessIdAsync(instalment.PaymentScheduleId, businessId);
            if (schedule == null)
                return ServiceResult.Fail("Payment schedule not found.");

            // Verify MatchedAmount == 0
            if (instalment.MatchedAmount != 0)
                return ServiceResult.Fail("Cannot remove an instalment that has matched payments.");

            // Delete instalment
            await _instalmentRepo.DeleteAsync(instalmentId);

            // Record history
            await _historyRepo.InsertAsync(new PaymentScheduleHistory
            {
                PaymentScheduleId = instalment.PaymentScheduleId,
                FieldChanged = "InstalmentRemoved",
                OldValue = $"#{instalment.SequenceNumber}: {instalment.Amount:N2}" + (instalment.DueDate.HasValue ? $" due {instalment.DueDate.Value:yyyy-MM-dd}" : ""),
                NewValue = null,
                ChangedByUserId = userId,
                ChangedAtUtc = DateTime.UtcNow
            });

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DeleteScheduleAsync(int scheduleId, int businessId, string userId)
    {
        try
        {
            // Get the schedule
            var schedule = await _scheduleRepo.GetByIdAndBusinessIdAsync(scheduleId, businessId);
            if (schedule == null)
                return ServiceResult.Fail("Payment schedule not found.");

            // Get all instalments
            var instalments = await _instalmentRepo.GetByScheduleIdAsync(scheduleId);

            // Verify ALL instalments have MatchedAmount == 0
            if (instalments.Any(i => i.MatchedAmount > 0))
                return ServiceResult.Fail("Cannot delete a schedule that has matched payments. Remove or void the payments first.");

            // Record "ScheduleDeleted" history entry before deletion
            await _historyRepo.InsertAsync(new PaymentScheduleHistory
            {
                PaymentScheduleId = scheduleId,
                FieldChanged = "ScheduleDeleted",
                OldValue = $"{instalments.Count} instalments",
                NewValue = null,
                ChangedByUserId = userId,
                ChangedAtUtc = DateTime.UtcNow
            });

            // Delete instalments then schedule
            await _instalmentRepo.DeleteByScheduleIdAsync(scheduleId);
            await _scheduleRepo.DeleteAsync(scheduleId);

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<PaymentScheduleDetailDto?> GetScheduleByInvoiceIdAsync(int invoiceId, int businessId)
    {
        try
        {
            var schedule = await _scheduleRepo.GetByInvoiceIdAsync(invoiceId, businessId);
            if (schedule == null)
                return null;

            var instalments = await _instalmentRepo.GetByScheduleIdAsync(schedule.Id);

            // Compute status for each instalment via InstalmentStatusEngine
            var instalmentDetails = instalments.Select(i =>
            {
                var statusId = _statusEngine.DetermineStatus(i.DueDate, i.Amount, i.MatchedAmount);
                return new InstalmentDetailDto
                {
                    Id = i.Id,
                    SequenceNumber = i.SequenceNumber,
                    Amount = i.Amount,
                    MatchedAmount = i.MatchedAmount,
                    DueDate = i.DueDate,
                    StatusId = statusId,
                    StatusName = StatusNames.GetValueOrDefault(statusId, "Unknown"),
                    ParentInstalmentId = i.ParentInstalmentId,
                    IsRemainder = i.IsRemainder,
                    PaymentId = i.PaymentId
                };
            }).ToList();

            // Build progress summary
            var totalPaid = instalmentDetails.Sum(i => i.MatchedAmount);
            var totalScheduleAmount = instalmentDetails.Sum(i => i.Amount);
            var completedCount = instalmentDetails.Count(i => i.StatusId == StatusPaid);

            return new PaymentScheduleDetailDto
            {
                Id = schedule.Id,
                InvoiceId = invoiceId,
                Instalments = instalmentDetails,
                TotalPaid = totalPaid,
                TotalRemaining = totalScheduleAmount - totalPaid,
                CompletedCount = completedCount,
                TotalCount = instalmentDetails.Count,
                CreatedAtUtc = schedule.CreatedAtUtc
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<PaymentScheduleHistoryDto>> GetScheduleHistoryAsync(int scheduleId, int businessId)
    {
        try
        {
            // Verify the schedule belongs to this business
            var schedule = await _scheduleRepo.GetByIdAndBusinessIdAsync(scheduleId, businessId);
            if (schedule == null)
                return new List<PaymentScheduleHistoryDto>();

            var historyEntries = await _historyRepo.GetByScheduleIdAsync(scheduleId);

            return historyEntries.Select(h => new PaymentScheduleHistoryDto
            {
                Id = h.Id,
                FieldChanged = h.FieldChanged,
                OldValue = h.OldValue,
                NewValue = h.NewValue,
                ChangedByUserId = h.ChangedByUserId,
                ChangedAtUtc = h.ChangedAtUtc
            }).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<VatWarningDto?> GetVatWarningAsync(int invoiceId, DateOnly? firstInstalmentDueDate, decimal firstInstalmentAmount, int businessId)
    {
        try
        {
            return await _vatWarningService.GetVatWarningAsync(invoiceId, firstInstalmentDueDate, firstInstalmentAmount, businessId);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task MatchPaymentToScheduleAsync(int paymentId, decimal paymentAmount, int invoiceId, int businessId, string userId)
    {
        try
        {
            // Get the schedule
            var schedule = await _scheduleRepo.GetByInvoiceIdAsync(invoiceId, businessId);
            if (schedule == null)
                return;

            // No need to re-fetch payment — amount is passed directly by the caller
            if (paymentAmount <= 0)
                return;

            // Get instalments
            var instalments = await _instalmentRepo.GetByScheduleIdAsync(schedule.Id);

            // Compute statuses and build InstalmentMatchCandidates
            var candidates = instalments
                .Select(i => new InstalmentMatchCandidate
                {
                    InstalmentId = i.Id,
                    Amount = i.Amount,
                    AlreadyMatched = i.MatchedAmount,
                    ComputedStatusId = _statusEngine.DetermineStatus(i.DueDate, i.Amount, i.MatchedAmount),
                    SequenceNumber = i.SequenceNumber
                })
                .Where(c => c.ComputedStatusId != StatusPaid) // Exclude already fully paid
                .ToList();

            if (candidates.Count == 0)
                return;

            // Call InstalmentMatchingEngine
            var matchResult = _matchingEngine.AllocatePayment(paymentAmount, candidates);

            // Apply allocations (update matched amounts)
            foreach (var allocation in matchResult.Allocations)
            {
                var instalment = instalments.First(i => i.Id == allocation.InstalmentId);
                var newMatchedAmount = instalment.MatchedAmount + allocation.AllocatedAmount;
                await _instalmentRepo.UpdateMatchedAmountAsync(allocation.InstalmentId, newMatchedAmount, paymentId);
            }

            // Create remainder instalment if needed
            if (matchResult.Remainder != null)
            {
                var maxSequence = instalments.Max(i => i.SequenceNumber);

                var remainderInstalment = new PaymentScheduleInstalment
                {
                    PaymentScheduleId = schedule.Id,
                    SequenceNumber = maxSequence + 1,
                    Amount = matchResult.Remainder.Amount,
                    MatchedAmount = 0,
                    DueDate = null,
                    PaymentId = paymentId,
                    ParentInstalmentId = matchResult.Remainder.ParentInstalmentId,
                    IsRemainder = true,
                    CreatedAtUtc = DateTime.UtcNow
                };

                await _instalmentRepo.InsertAsync(remainderInstalment);
            }

            // Recalculate invoice financial status
            await _financialStatusEngine.RecalculateStatusAsync(invoiceId, businessId);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task RevertPaymentMatchAsync(int paymentId, int invoiceId, int businessId)
    {
        try
        {
            // Get the schedule
            var schedule = await _scheduleRepo.GetByInvoiceIdAsync(invoiceId, businessId);
            if (schedule == null)
                return;

            // Get instalments
            var instalments = await _instalmentRepo.GetByScheduleIdAsync(schedule.Id);

            // Find instalments matched to the payment (PaymentId == paymentId)
            var matchedInstalments = instalments.Where(i => i.PaymentId == paymentId).ToList();

            // Reset MatchedAmount to 0 and clear PaymentId
            foreach (var instalment in matchedInstalments)
            {
                await _instalmentRepo.UpdateMatchedAmountAsync(instalment.Id, 0, null);
            }

            // Delete any remainder instalments derived from that payment
            var remainderInstalments = instalments.Where(i => i.IsRemainder && i.PaymentId == paymentId).ToList();
            foreach (var remainder in remainderInstalments)
            {
                await _instalmentRepo.DeleteAsync(remainder.Id);
            }

            // Recalculate invoice financial status
            await _financialStatusEngine.RecalculateStatusAsync(invoiceId, businessId);
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
