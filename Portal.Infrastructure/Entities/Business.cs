namespace Portal.Infrastructure.Entities;

/// <summary>
/// The tenant entity representing a subscribing company within the platform.
/// Schema: [portal].Business
/// </summary>
public class Business
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; }

    public bool IsDemoAccount { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public bool IsPaymentInstructionsEnabled { get; set; }

    // Navigation properties
    public BusinessProfile? BusinessProfile { get; set; }

    public ICollection<Customer> Customers { get; set; } = new List<Customer>();

    public ICollection<Quotation> Quotations { get; set; } = new List<Quotation>();

    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public ICollection<Supplier> Suppliers { get; set; } = new List<Supplier>();

    public ICollection<ExpenseCategory> ExpenseCategories { get; set; } = new List<ExpenseCategory>();

    public ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();

    public ICollection<VatSubmissionPeriod> VatSubmissionPeriods { get; set; } = new List<VatSubmissionPeriod>();

    public ICollection<VatSubmission> VatSubmissions { get; set; } = new List<VatSubmission>();

    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public ICollection<ProposalShare> ProposalShares { get; set; } = new List<ProposalShare>();

    public ICollection<BusinessLogo> BusinessLogos { get; set; } = new List<BusinessLogo>();

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
