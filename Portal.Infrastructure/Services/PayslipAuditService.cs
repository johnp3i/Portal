using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models.Payroll;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

public class PayslipAuditService : IPayslipAuditService
{
    private readonly PayrollRepository _payrollRepository;

    public PayslipAuditService(PayrollRepository payrollRepository)
    {
        _payrollRepository = payrollRepository;
    }

    public async Task RecordStatusChangeAsync(int payslipId, string userId, byte actionTypeId)
    {
        try
        {
            await _payrollRepository.InsertAuditLogAsync(new PayslipAuditLog
            {
                PayslipId = payslipId,
                UserId = userId,
                PayslipAuditActionTypeId = actionTypeId,
                FieldName = null,
                OldValue = null,
                NewValue = null
            });
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task RecordEarningLineChangesAsync(
        int payslipId,
        string userId,
        List<PayslipEarningLine> oldLines,
        List<PayslipEarningLine> newLines,
        List<EarningType> earningTypes)
    {
        try
        {
            var entries = new List<PayslipAuditLog>();
            var earningTypeNames = earningTypes.ToDictionary(e => e.Id, e => e.Name);

            // Group old lines by EarningTypeId to detect duplicates
            var oldByType = oldLines
                .GroupBy(l => l.EarningTypeId)
                .ToDictionary(g => g.Key, g => g.OrderBy(l => l.Id).ToList());

            var newByType = newLines
                .GroupBy(l => l.EarningTypeId)
                .ToDictionary(g => g.Key, g => g.OrderBy(l => l.Id).ToList());

            // Detect removals (types in old but not in new)
            foreach (var kvp in oldByType)
            {
                if (!newByType.ContainsKey(kvp.Key))
                {
                    var typeName = earningTypeNames.GetValueOrDefault(kvp.Key, $"Type{kvp.Key}");
                    var hasDuplicates = kvp.Value.Count > 1;

                    for (int i = 0; i < kvp.Value.Count; i++)
                    {
                        var fieldName = hasDuplicates
                            ? $"EarningLine:{typeName}[{i}]"
                            : $"EarningLine:{typeName}";

                        entries.Add(new PayslipAuditLog
                        {
                            PayslipId = payslipId,
                            UserId = userId,
                            PayslipAuditActionTypeId = 2, // Edited
                            FieldName = fieldName,
                            OldValue = kvp.Value[i].Amount.ToString("0.00"),
                            NewValue = null
                        });
                    }
                }
            }

            // Detect additions (types in new but not in old)
            foreach (var kvp in newByType)
            {
                if (!oldByType.ContainsKey(kvp.Key))
                {
                    var typeName = earningTypeNames.GetValueOrDefault(kvp.Key, $"Type{kvp.Key}");
                    var hasDuplicates = kvp.Value.Count > 1;

                    for (int i = 0; i < kvp.Value.Count; i++)
                    {
                        var fieldName = hasDuplicates
                            ? $"EarningLine:{typeName}[{i}]"
                            : $"EarningLine:{typeName}";

                        entries.Add(new PayslipAuditLog
                        {
                            PayslipId = payslipId,
                            UserId = userId,
                            PayslipAuditActionTypeId = 2, // Edited
                            FieldName = fieldName,
                            OldValue = null,
                            NewValue = kvp.Value[i].Amount.ToString("0.00")
                        });
                    }
                }
            }

            // Detect modifications (types in both old and new)
            foreach (var kvp in oldByType)
            {
                if (newByType.TryGetValue(kvp.Key, out var newList))
                {
                    var oldList = kvp.Value;
                    var typeName = earningTypeNames.GetValueOrDefault(kvp.Key, $"Type{kvp.Key}");
                    var maxCount = Math.Max(oldList.Count, newList.Count);
                    var hasDuplicates = maxCount > 1;

                    for (int i = 0; i < maxCount; i++)
                    {
                        decimal? oldAmount = i < oldList.Count ? oldList[i].Amount : null;
                        decimal? newAmount = i < newList.Count ? newList[i].Amount : null;

                        if (oldAmount != newAmount)
                        {
                            var fieldName = hasDuplicates
                                ? $"EarningLine:{typeName}[{i}]:Amount"
                                : $"EarningLine:{typeName}:Amount";

                            entries.Add(new PayslipAuditLog
                            {
                                PayslipId = payslipId,
                                UserId = userId,
                                PayslipAuditActionTypeId = 2, // Edited
                                FieldName = fieldName,
                                OldValue = oldAmount?.ToString("0.00"),
                                NewValue = newAmount?.ToString("0.00")
                            });
                        }
                    }
                }
            }

            if (entries.Count > 0)
            {
                await _payrollRepository.InsertAuditLogBatchAsync(entries);
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task RecordManagerNotesChangeAsync(int payslipId, string userId, string? oldNotes, string? newNotes)
    {
        try
        {
            if (oldNotes == newNotes) return;

            await _payrollRepository.InsertAuditLogAsync(new PayslipAuditLog
            {
                PayslipId = payslipId,
                UserId = userId,
                PayslipAuditActionTypeId = 2, // Edited
                FieldName = "ManagerNotes",
                OldValue = oldNotes?.Length > 500 ? oldNotes[..500] : oldNotes,
                NewValue = newNotes?.Length > 500 ? newNotes[..500] : newNotes
            });
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task RecordPayslipAddedOrRemovedAsync(int payslipId, string userId, bool isAdded, string employeeName)
    {
        try
        {
            await _payrollRepository.InsertAuditLogAsync(new PayslipAuditLog
            {
                PayslipId = payslipId,
                UserId = userId,
                PayslipAuditActionTypeId = 2, // Edited
                FieldName = "Payslip",
                OldValue = isAdded ? null : employeeName,
                NewValue = isAdded ? employeeName : null
            });
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<PayslipAuditLogDto>> GetAuditHistoryAsync(int payslipId, int businessId)
    {
        try
        {
            return await _payrollRepository.GetAuditLogsByPayslipAsync(payslipId);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<PeriodAuditGroupDto>> GetPeriodAuditSummaryAsync(int periodId, int businessId)
    {
        try
        {
            var allEntries = await _payrollRepository.GetAuditLogsByPeriodAsync(periodId);

            var grouped = allEntries
                .GroupBy(e => new { e.PayslipId, e.EmployeeName })
                .Select(g => new PeriodAuditGroupDto
                {
                    PayslipId = g.Key.PayslipId,
                    EmployeeName = g.Key.EmployeeName ?? "Unknown",
                    Entries = g.ToList()
                })
                .ToList();

            return grouped;
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
