using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Services;

namespace Portal.Infrastructure.Data;

/// <summary>
/// The main DbContext for the Portal database. Configures all entity mappings,
/// relationships, indexes, constraints, and default values via Fluent API.
/// </summary>
public class PortalDbContext : DbContext
{
    private readonly ICurrentTenantService _currentTenantService;

    public PortalDbContext(DbContextOptions<PortalDbContext> options, ICurrentTenantService currentTenantService)
        : base(options)
    {
        _currentTenantService = currentTenantService;
    }

    // Portal schema
    public DbSet<Business> Businesses { get; set; } = null!;
    public DbSet<BusinessProfile> BusinessProfiles { get; set; } = null!;

    // Customer schema
    public DbSet<Customer> Customers { get; set; } = null!;

    // Quotation schema
    public DbSet<QuotationStatusType> QuotationStatusTypes { get; set; } = null!;
    public DbSet<Quotation> Quotations { get; set; } = null!;
    public DbSet<QuotationLine> QuotationLines { get; set; } = null!;

    // Invoice schema
    public DbSet<InvoiceStatusType> InvoiceStatusTypes { get; set; } = null!;
    public DbSet<InvoiceFinancialStatusType> InvoiceFinancialStatusTypes { get; set; } = null!;
    public DbSet<Invoice> Invoices { get; set; } = null!;
    public DbSet<InvoiceLine> InvoiceLines { get; set; } = null!;

    // Revenue schema
    public DbSet<PaymentMethodType> PaymentMethodTypes { get; set; } = null!;
    public DbSet<Payment> Payments { get; set; } = null!;

    // Purchase schema
    public DbSet<Supplier> Suppliers { get; set; } = null!;
    public DbSet<ExpenseCategory> ExpenseCategories { get; set; } = null!;
    public DbSet<Purchase> Purchases { get; set; } = null!;

    // VAT schema
    public DbSet<VatSubmissionPeriod> VatSubmissionPeriods { get; set; } = null!;
    public DbSet<VatSubmission> VatSubmissions { get; set; } = null!;

    // Proposal schema
    public DbSet<ProposalShare> ProposalShares { get; set; } = null!;
    public DbSet<ProposalSection> ProposalSections { get; set; } = null!;
    public DbSet<BusinessLogo> BusinessLogos { get; set; } = null!;
    public DbSet<QuotationContact> QuotationContacts { get; set; } = null!;
    public DbSet<LineItemCatalog> LineItemCatalogs { get; set; } = null!;

    // Audit schema
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureBusiness(modelBuilder);
        ConfigureBusinessProfile(modelBuilder);
        ConfigureCustomer(modelBuilder);
        ConfigureQuotationStatusType(modelBuilder);
        ConfigureQuotation(modelBuilder);
        ConfigureQuotationLine(modelBuilder);
        ConfigureInvoiceStatusType(modelBuilder);
        ConfigureInvoiceFinancialStatusType(modelBuilder);
        ConfigureInvoice(modelBuilder);
        ConfigureInvoiceLine(modelBuilder);
        ConfigurePaymentMethodType(modelBuilder);
        ConfigurePayment(modelBuilder);
        ConfigureSupplier(modelBuilder);
        ConfigureExpenseCategory(modelBuilder);
        ConfigurePurchase(modelBuilder);
        ConfigureVatSubmissionPeriod(modelBuilder);
        ConfigureVatSubmission(modelBuilder);
        ConfigureAuditLog(modelBuilder);
        ConfigureProposalShare(modelBuilder);
        ConfigureProposalSection(modelBuilder);
        ConfigureBusinessLogo(modelBuilder);
        ConfigureQuotationContact(modelBuilder);
        ConfigureLineItemCatalog(modelBuilder);

        ApplyGlobalQueryFilters(modelBuilder);
    }

    private static void ConfigureBusiness(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Business>(entity =>
        {
            entity.ToTable("Business", "portal");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.HasIndex(e => e.Name)
                .IsUnique();

            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.UpdatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    private static void ConfigureBusinessProfile(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BusinessProfile>(entity =>
        {
            entity.ToTable("BusinessProfile", "portal");

            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Business)
                .WithOne(b => b.BusinessProfile)
                .HasForeignKey<BusinessProfile>(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(e => e.BusinessId)
                .IsUnique();

            entity.Property(e => e.CompanyRegistrationNumber)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.VatRegistrationNumber)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.VatRegistrationDate)
                .IsRequired();

            entity.Property(e => e.VatPeriodLengthInMonths)
                .IsRequired();

            entity.ToTable(t => t.HasCheckConstraint(
                "CK_BusinessProfile_VatPeriodLengthInMonths",
                "[VatPeriodLengthInMonths] IN (1, 2, 3, 4, 6, 12)"));

            entity.Property(e => e.AddressLine1)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.AddressLine2)
                .HasMaxLength(200);

            entity.Property(e => e.City)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.PostalCode)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(e => e.Country)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.TelephoneNumber)
                .HasMaxLength(30);

            entity.Property(e => e.MobileNumber)
                .HasMaxLength(30);

            entity.Property(e => e.Email)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.CurrencySymbol)
                .IsRequired()
                .HasMaxLength(5)
                .HasDefaultValue("€");
        });
    }

    private static void ConfigureCustomer(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customer", "customer");

            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Business)
                .WithMany(b => b.Customers)
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(e => e.BusinessId)
                .HasDatabaseName("IX_Customer_BusinessId");

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.ContactPerson)
                .HasMaxLength(200);

            entity.Property(e => e.Email)
                .HasMaxLength(200);

            entity.Property(e => e.TelephoneNumber)
                .HasMaxLength(30);

            entity.Property(e => e.MobileNumber)
                .HasMaxLength(30);

            entity.Property(e => e.AddressLine1)
                .HasMaxLength(200);

            entity.Property(e => e.AddressLine2)
                .HasMaxLength(200);

            entity.Property(e => e.City)
                .HasMaxLength(100);

            entity.Property(e => e.PostalCode)
                .HasMaxLength(20);

            entity.Property(e => e.Country)
                .HasMaxLength(100);

            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.UpdatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    private static void ConfigureQuotationStatusType(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QuotationStatusType>(entity =>
        {
            entity.ToTable("QuotationStatusType", "quotation");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasData(
                new QuotationStatusType { Id = 1, Name = "Draft" },
                new QuotationStatusType { Id = 2, Name = "Sent" },
                new QuotationStatusType { Id = 3, Name = "Accepted" },
                new QuotationStatusType { Id = 4, Name = "Converted" },
                new QuotationStatusType { Id = 5, Name = "Archived" }
            );
        });
    }

    private static void ConfigureQuotation(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Quotation>(entity =>
        {
            entity.ToTable("Quotation", "quotation");

            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Business)
                .WithMany(b => b.Quotations)
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.Customer)
                .WithMany(c => c.Quotations)
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.QuotationStatusType)
                .WithMany(s => s.Quotations)
                .HasForeignKey(e => e.QuotationStatusTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.QuotationContact)
                .WithMany()
                .HasForeignKey(e => e.QuotationContactId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(e => e.BusinessId)
                .HasDatabaseName("IX_Quotation_BusinessId");

            entity.Property(e => e.Reference)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Subtotal)
                .HasPrecision(18, 2);

            entity.Property(e => e.TaxAmount)
                .HasPrecision(18, 2);

            entity.Property(e => e.TotalAmount)
                .HasPrecision(18, 2);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.UpdatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    private static void ConfigureQuotationLine(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QuotationLine>(entity =>
        {
            entity.ToTable("QuotationLine", "quotation");

            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Quotation)
                .WithMany(q => q.QuotationLines)
                .HasForeignKey(e => e.QuotationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ProposalSection)
                .WithMany(s => s.QuotationLines)
                .HasForeignKey(e => e.ProposalSectionId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            entity.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.Quantity)
                .HasPrecision(18, 4);

            entity.Property(e => e.UnitPrice)
                .HasPrecision(18, 2);

            entity.Property(e => e.VatRate)
                .HasPrecision(5, 2);

            entity.Property(e => e.Discount)
                .HasPrecision(5, 2)
                .HasDefaultValue(0m);

            entity.Property(e => e.DiscountType)
                .IsRequired()
                .HasMaxLength(10)
                .HasDefaultValue("Percentage");

            entity.Property(e => e.LineTotal)
                .HasPrecision(18, 2);

            entity.Property(e => e.ReferenceUrl)
                .HasMaxLength(2048);

            entity.Property(e => e.CostPrice)
                .HasPrecision(18, 2);

            entity.Property(e => e.Subtitle)
                .HasMaxLength(1000);
        });
    }

    private static void ConfigureInvoiceStatusType(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InvoiceStatusType>(entity =>
        {
            entity.ToTable("InvoiceStatusType", "invoice");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasData(
                new InvoiceStatusType { Id = 1, Name = "Draft" },
                new InvoiceStatusType { Id = 2, Name = "Issued" },
                new InvoiceStatusType { Id = 3, Name = "Cancelled" }
            );
        });
    }

    private static void ConfigureInvoiceFinancialStatusType(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InvoiceFinancialStatusType>(entity =>
        {
            entity.ToTable("InvoiceFinancialStatusType", "invoice");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasData(
                new InvoiceFinancialStatusType { Id = 1, Name = "Unpaid" },
                new InvoiceFinancialStatusType { Id = 2, Name = "PartiallyPaid" },
                new InvoiceFinancialStatusType { Id = 3, Name = "Paid" },
                new InvoiceFinancialStatusType { Id = 4, Name = "Overdue" },
                new InvoiceFinancialStatusType { Id = 5, Name = "WrittenOff" }
            );
        });
    }

    private static void ConfigureInvoice(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.ToTable("Invoice", "invoice");

            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Business)
                .WithMany(b => b.Invoices)
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.Customer)
                .WithMany(c => c.Invoices)
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.Quotation)
                .WithMany(q => q.Invoices)
                .HasForeignKey(e => e.QuotationId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.InvoiceStatusType)
                .WithMany(s => s.Invoices)
                .HasForeignKey(e => e.InvoiceStatusTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.InvoiceFinancialStatusType)
                .WithMany(s => s.Invoices)
                .HasForeignKey(e => e.InvoiceFinancialStatusTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(e => e.BusinessId)
                .HasDatabaseName("IX_Invoice_BusinessId");

            entity.HasIndex(e => e.QuotationId)
                .IsUnique()
                .HasDatabaseName("UX_Invoice_QuotationId")
                .HasFilter("[QuotationId] IS NOT NULL");

            entity.Property(e => e.InvoiceNumber)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.Subtotal)
                .HasPrecision(18, 2);

            entity.Property(e => e.TaxAmount)
                .HasPrecision(18, 2);

            entity.Property(e => e.TotalAmount)
                .HasPrecision(18, 2);

            entity.Property(e => e.CurrencyCode)
                .IsRequired()
                .HasMaxLength(3)
                .HasDefaultValue("EUR");

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.UpdatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    private static void ConfigureInvoiceLine(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InvoiceLine>(entity =>
        {
            entity.ToTable("InvoiceLine", "invoice");

            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Invoice)
                .WithMany(i => i.InvoiceLines)
                .HasForeignKey(e => e.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.Quantity)
                .HasPrecision(18, 4);

            entity.Property(e => e.UnitPrice)
                .HasPrecision(18, 2);

            entity.Property(e => e.LineTotal)
                .HasPrecision(18, 2);
        });
    }

    private static void ConfigurePaymentMethodType(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PaymentMethodType>(entity =>
        {
            entity.ToTable("PaymentMethodType", "revenue");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            entity.HasData(
                new PaymentMethodType { Id = 1, Name = "Cash", IsActive = true },
                new PaymentMethodType { Id = 2, Name = "BankTransfer", IsActive = true },
                new PaymentMethodType { Id = 3, Name = "Card", IsActive = true },
                new PaymentMethodType { Id = 4, Name = "Cheque", IsActive = true },
                new PaymentMethodType { Id = 5, Name = "Other", IsActive = true }
            );
        });
    }

    private static void ConfigurePayment(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("Payment", "revenue");

            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Business)
                .WithMany(b => b.Payments)
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.Invoice)
                .WithMany(i => i.Payments)
                .HasForeignKey(e => e.InvoiceId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.PaymentMethodType)
                .WithMany(p => p.Payments)
                .HasForeignKey(e => e.PaymentMethodTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(e => e.BusinessId)
                .HasDatabaseName("IX_Payment_BusinessId");

            entity.Property(e => e.Amount)
                .HasPrecision(18, 2);

            entity.Property(e => e.Reference)
                .HasMaxLength(200);

            entity.Property(e => e.IsVoided)
                .IsRequired()
                .HasDefaultValue(false);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.CreatedByUserId)
                .HasMaxLength(450);
        });
    }

    private static void ConfigureSupplier(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.ToTable("Supplier", "purchase");

            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Business)
                .WithMany(b => b.Suppliers)
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(e => e.BusinessId)
                .HasDatabaseName("IX_Supplier_BusinessId");

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    private static void ConfigureExpenseCategory(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExpenseCategory>(entity =>
        {
            entity.ToTable("ExpenseCategory", "purchase");

            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Business)
                .WithMany(b => b.ExpenseCategories)
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(e => e.BusinessId)
                .HasDatabaseName("IX_ExpenseCategory_BusinessId");

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true);
        });
    }

    private static void ConfigurePurchase(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Purchase>(entity =>
        {
            entity.ToTable("Purchase", "purchase");

            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Business)
                .WithMany(b => b.Purchases)
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.Supplier)
                .WithMany(s => s.Purchases)
                .HasForeignKey(e => e.SupplierId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.ExpenseCategory)
                .WithMany(c => c.Purchases)
                .HasForeignKey(e => e.ExpenseCategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(e => e.BusinessId)
                .HasDatabaseName("IX_Purchase_BusinessId");

            entity.Property(e => e.InvoiceNumber)
                .HasMaxLength(100);

            entity.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.AmountExcludingVat)
                .HasPrecision(18, 2);

            entity.Property(e => e.VatAmount)
                .HasPrecision(18, 2);

            entity.Property(e => e.TotalAmount)
                .HasPrecision(18, 2);

            entity.Property(e => e.IsEuReverseCharge)
                .IsRequired()
                .HasDefaultValue(false);

            entity.Property(e => e.Country)
                .HasMaxLength(100);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.UpdatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    private static void ConfigureVatSubmissionPeriod(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VatSubmissionPeriod>(entity =>
        {
            entity.ToTable("VatSubmissionPeriod", "vat");

            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Business)
                .WithMany(b => b.VatSubmissionPeriods)
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(e => e.BusinessId)
                .HasDatabaseName("IX_VatSubmissionPeriod_BusinessId");

            entity.HasIndex(e => new { e.BusinessId, e.PeriodStartDate })
                .IsUnique()
                .HasDatabaseName("UX_VatSubmissionPeriod_BusinessId_PeriodStartDate");

            entity.Property(e => e.PeriodLabel)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    private static void ConfigureVatSubmission(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VatSubmission>(entity =>
        {
            entity.ToTable("VatSubmission", "vat");

            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Business)
                .WithMany(b => b.VatSubmissions)
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.VatSubmissionPeriod)
                .WithMany(p => p.VatSubmissions)
                .HasForeignKey(e => e.VatSubmissionPeriodId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(e => e.BusinessId)
                .HasDatabaseName("IX_VatSubmission_BusinessId");

            entity.HasIndex(e => new { e.BusinessId, e.VatSubmissionPeriodId })
                .IsUnique()
                .HasDatabaseName("UX_VatSubmission_BusinessId_VatSubmissionPeriodId");

            entity.Property(e => e.TotalOutputVat)
                .HasPrecision(18, 2);

            entity.Property(e => e.TotalInputVat)
                .HasPrecision(18, 2);

            entity.Property(e => e.NetVatPayable)
                .HasPrecision(18, 2);

            entity.Property(e => e.IsSubmitted)
                .IsRequired()
                .HasDefaultValue(false);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    private static void ConfigureAuditLog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLog", "audit");

            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Business)
                .WithMany(b => b.AuditLogs)
                .HasForeignKey(e => e.BusinessId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(e => e.BusinessId)
                .HasDatabaseName("IX_AuditLog_BusinessId");

            entity.Property(e => e.UserId)
                .HasMaxLength(450);

            entity.Property(e => e.Action)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.TableName)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.RecordId)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.Timestamp)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    private static void ConfigureProposalShare(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProposalShare>(entity =>
        {
            entity.ToTable("ProposalShare", "quotation");

            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Quotation)
                .WithMany()
                .HasForeignKey(e => e.QuotationId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.Business)
                .WithMany(b => b.ProposalShares)
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(e => e.QuotationId)
                .HasDatabaseName("IX_ProposalShare_QuotationId");

            entity.HasIndex(e => e.BusinessId)
                .HasDatabaseName("IX_ProposalShare_BusinessId");

            entity.HasIndex(e => e.ShareToken)
                .IsUnique()
                .HasDatabaseName("UX_ProposalShare_ShareToken");

            entity.Property(e => e.ShareToken)
                .IsRequired()
                .HasMaxLength(128);

            entity.Property(e => e.SnapshotHtml)
                .IsRequired();

            entity.Property(e => e.CustomerEmail)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("SYSDATETIMEOFFSET()");

            entity.Property(e => e.CreatedByUserId)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true);
        });
    }

    private static void ConfigureProposalSection(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProposalSection>(entity =>
        {
            entity.ToTable("ProposalSection", "quotation");

            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Quotation)
                .WithMany()
                .HasForeignKey(e => e.QuotationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.QuotationId)
                .HasDatabaseName("IX_ProposalSection_QuotationId");

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.ColumnConfiguration)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("OneTime");

            entity.Property(e => e.Description)
                .HasMaxLength(2000);

            entity.Property(e => e.Notes)
                .HasMaxLength(4000);

            entity.Property(e => e.SectionType)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("LineItems");

            entity.Property(e => e.IsEmphasized)
                .IsRequired()
                .HasDefaultValue(false);

            entity.Property(e => e.AccentColor)
                .HasMaxLength(20);

            entity.Property(e => e.Label)
                .HasMaxLength(50);
        });
    }

    private static void ConfigureLineItemCatalog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LineItemCatalog>(entity =>
        {
            entity.ToTable("LineItemCatalog", "quotation");
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Business)
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.BusinessId)
                .HasDatabaseName("IX_LineItemCatalog_BusinessId");

            entity.HasIndex(e => new { e.BusinessId, e.Description })
                .IsUnique()
                .HasDatabaseName("UQ_LineItemCatalog_Business_Description");

            entity.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.UnitPrice)
                .HasColumnType("decimal(18,2)");

            entity.Property(e => e.VatRate)
                .HasColumnType("decimal(5,2)");

            entity.Property(e => e.ReferenceUrl)
                .HasMaxLength(2048);

            entity.Property(e => e.Discount)
                .HasColumnType("decimal(18,2)")
                .HasDefaultValue(0m);

            entity.Property(e => e.DiscountType)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("Percentage");

            entity.Property(e => e.UpdatedAtUtc)
                .HasDefaultValueSql("SYSUTCDATETIME()");
        });
    }

    private static void ConfigureBusinessLogo(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BusinessLogo>(entity =>
        {
            entity.ToTable("BusinessLogo", "portal");

            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Business)
                .WithMany(b => b.BusinessLogos)
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(e => e.BusinessId)
                .HasDatabaseName("IX_BusinessLogo_BusinessId");

            entity.Property(e => e.DisplayName)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.FileName)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.ContentType)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.PublicUrl)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.IsPrimary)
                .IsRequired()
                .HasDefaultValue(false);
        });
    }

    private static void ConfigureQuotationContact(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QuotationContact>(entity =>
        {
            entity.ToTable("QuotationContact", "quotation");

            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Business)
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(e => e.BusinessId)
                .HasDatabaseName("IX_QuotationContact_BusinessId");

            entity.Property(e => e.UserId)
                .HasMaxLength(450);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Email)
                .HasMaxLength(200);

            entity.Property(e => e.TelephoneNumber)
                .HasMaxLength(30);

            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    /// <summary>
    /// Applies global query filters on BusinessId for all tenant-scoped entities.
    /// Reference tables (QuotationStatusType, InvoiceStatusType, InvoiceFinancialStatusType, PaymentMethodType)
    /// and the Business entity itself are excluded.
    /// QuotationLine and InvoiceLine do not have their own BusinessId — they inherit tenant scope from their parent.
    /// </summary>
    private void ApplyGlobalQueryFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId);

        modelBuilder.Entity<Quotation>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId);

        modelBuilder.Entity<Invoice>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId);

        modelBuilder.Entity<Payment>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId);

        modelBuilder.Entity<Supplier>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId);

        modelBuilder.Entity<ExpenseCategory>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId);

        modelBuilder.Entity<Purchase>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId);

        modelBuilder.Entity<VatSubmissionPeriod>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId);

        modelBuilder.Entity<VatSubmission>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId);

        // AuditLog has nullable BusinessId — filter still applies for tenant-scoped queries
        modelBuilder.Entity<AuditLog>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId);

        modelBuilder.Entity<ProposalShare>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId);

        modelBuilder.Entity<BusinessLogo>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId);

        modelBuilder.Entity<QuotationContact>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId);

        modelBuilder.Entity<LineItemCatalog>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId);
    }
}
