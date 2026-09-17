namespace Portal.Infrastructure.Models;

/// <summary>
/// Result of evaluating whether an invoice may currently be edited.
///
/// An invoice is editable when it is in Draft status, OR when it is Issued AND has no recorded
/// settlement (no non-voided payments and no applied non-voided credit notes) AND its assigned
/// VAT submission period has not been filed. See the invoice-edit spec / financial-conventions
/// steering for the full rule.
/// </summary>
/// <param name="CanEdit">True when the invoice may be edited.</param>
/// <param name="Reason">Human-readable reason when <paramref name="CanEdit"/> is false; null otherwise.</param>
/// <param name="IsIssued">True when the invoice is in Issued status (as opposed to Draft).</param>
public sealed record InvoiceEditEligibility(bool CanEdit, string? Reason, bool IsIssued);
