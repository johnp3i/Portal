namespace Portal.Infrastructure.Models;

/// <summary>
/// Input for manually applying an account-level credit note's available balance across one or more
/// of the customer's outstanding invoices. Each allocation targets a specific invoice and amount.
/// </summary>
public class ApplyAccountCreditDto
{
    public int CreditNoteId { get; set; }
    public List<AccountCreditAllocationDto> Allocations { get; set; } = new();
}

/// <summary>
/// A single invoice/amount pair within an account-credit application request.
/// </summary>
public class AccountCreditAllocationDto
{
    public int InvoiceId { get; set; }
    public decimal Amount { get; set; }
}
