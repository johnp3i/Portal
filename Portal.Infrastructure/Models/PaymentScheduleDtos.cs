namespace Portal.Infrastructure.Models;

// === Request DTOs ===

/// <summary>
/// Input DTO for creating a new payment schedule against an invoice.
/// </summary>
public class CreatePaymentScheduleDto
{
    public int InvoiceId { get; set; }
    public List<CreateInstalmentDto> Instalments { get; set; } = new();
}

/// <summary>
/// Input DTO for a single instalment within a new payment schedule.
/// </summary>
public class CreateInstalmentDto
{
    public decimal Amount { get; set; }
    public DateOnly? DueDate { get; set; }
}

/// <summary>
/// Input DTO for modifying an existing instalment's amount or due date.
/// </summary>
public class UpdateInstalmentDto
{
    public int InstalmentId { get; set; }
    public int ScheduleId { get; set; }
    public decimal? NewAmount { get; set; }
    public DateOnly? NewDueDate { get; set; }
    public bool ClearDueDate { get; set; }
}

/// <summary>
/// Input DTO for adding a new instalment to an existing payment schedule.
/// </summary>
public class AddInstalmentDto
{
    public int ScheduleId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly? DueDate { get; set; }
}

// === Response DTOs ===

/// <summary>
/// Response DTO containing full payment schedule detail with progress summary.
/// </summary>
public class PaymentScheduleDetailDto
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public List<InstalmentDetailDto> Instalments { get; set; } = new();
    public decimal TotalPaid { get; set; }
    public decimal TotalRemaining { get; set; }
    public int CompletedCount { get; set; }
    public int TotalCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// Response DTO for a single instalment within a payment schedule.
/// </summary>
public class InstalmentDetailDto
{
    public int Id { get; set; }
    public int SequenceNumber { get; set; }
    public decimal Amount { get; set; }
    public decimal MatchedAmount { get; set; }
    public DateOnly? DueDate { get; set; }
    public int StatusId { get; set; }
    public string StatusName { get; set; } = null!;
    public int? ParentInstalmentId { get; set; }
    public bool IsRemainder { get; set; }
    public int? PaymentId { get; set; }
}

/// <summary>
/// Response DTO for a single modification history entry on a payment schedule.
/// </summary>
public class PaymentScheduleHistoryDto
{
    public int Id { get; set; }
    public string FieldChanged { get; set; } = null!;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string ChangedByUserId { get; set; } = null!;
    public DateTime ChangedAtUtc { get; set; }
}

/// <summary>
/// Response DTO for VAT deadline conflict warnings during schedule creation.
/// </summary>
public class VatWarningDto
{
    public bool ShowWarning { get; set; }
    public bool HighlightVatAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public DateOnly SubmissionDeadline { get; set; }
    public string Message { get; set; } = null!;
}
