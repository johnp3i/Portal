using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Entities.Billing;
using Portal.Infrastructure.Entities.Sales;
using Portal.Infrastructure.Entities.Stripe;
using Portal.Infrastructure.Models;
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
    public DbSet<BusinessPaymentDetail> BusinessPaymentDetails { get; set; } = null!;

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
    public DbSet<InvoiceShare> InvoiceShares { get; set; } = null!;
    public DbSet<InvoiceAcceptance> InvoiceAcceptances { get; set; } = null!;

    // Revenue schema
    public DbSet<PaymentMethodType> PaymentMethodTypes { get; set; } = null!;
    public DbSet<Payment> Payments { get; set; } = null!;

    // Payment Schedule schema (revenue)
    public DbSet<PaymentSchedule> PaymentSchedules { get; set; } = null!;
    public DbSet<PaymentScheduleInstalment> PaymentScheduleInstalments { get; set; } = null!;
    public DbSet<PaymentScheduleInstalmentStatusType> PaymentScheduleInstalmentStatusTypes { get; set; } = null!;
    public DbSet<PaymentScheduleHistory> PaymentScheduleHistories { get; set; } = null!;

    // Purchase schema
    public DbSet<Supplier> Suppliers { get; set; } = null!;
    public DbSet<ExpenseCategory> ExpenseCategories { get; set; } = null!;
    public DbSet<ExpenseCategoryTemplate> ExpenseCategoryTemplates { get; set; } = null!;
    public DbSet<Purchase> Purchases { get; set; } = null!;
    public DbSet<PurchaseOriginType> PurchaseOriginTypes { get; set; } = null!;
    public DbSet<ExpenseType> ExpenseTypes { get; set; } = null!;
    public DbSet<PurchaseType> PurchaseTypes { get; set; } = null!;
    public DbSet<ExpenseCategoryLimit> ExpenseCategoryLimits { get; set; } = null!;

    // VAT schema
    public DbSet<VatSubmissionPeriod> VatSubmissionPeriods { get; set; } = null!;
    public DbSet<VatSubmission> VatSubmissions { get; set; } = null!;

    // Proposal schema
    public DbSet<ProposalShare> ProposalShares { get; set; } = null!;
    public DbSet<ProposalAcceptance> ProposalAcceptances { get; set; } = null!;
    public DbSet<ProposalSection> ProposalSections { get; set; } = null!;
    public DbSet<BusinessLogo> BusinessLogos { get; set; } = null!;
    public DbSet<QuotationContact> QuotationContacts { get; set; } = null!;
    public DbSet<LineItemCatalog> LineItemCatalogs { get; set; } = null!;

    // Product schema
    public DbSet<Product> Products { get; set; } = null!;
    public DbSet<ProductPriceHistory> ProductPriceHistories { get; set; } = null!;
    public DbSet<ProductPriceTier> ProductPriceTiers { get; set; } = null!;

    // Credit schema
    public DbSet<CreditNoteStatusType> CreditNoteStatusTypes { get; set; } = null!;
    public DbSet<CreditNote> CreditNotes { get; set; } = null!;
    public DbSet<CreditNoteLine> CreditNoteLines { get; set; } = null!;
    public DbSet<CreditNoteApplication> CreditNoteApplications { get; set; } = null!;

    // Audit schema
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;

    // Subscription plan schema
    public DbSet<Plan> Plans { get; set; } = null!;
    public DbSet<PlanFeature> PlanFeatures { get; set; } = null!;
    public DbSet<BusinessPlan> BusinessPlans { get; set; } = null!;

    // Billing schema
    public DbSet<Subscription> Subscriptions { get; set; } = null!;
    public DbSet<BillingInvoice> BillingInvoices { get; set; } = null!;
    public DbSet<BillingPayment> BillingPayments { get; set; } = null!;
    public DbSet<InvoiceSequence> InvoiceSequences { get; set; } = null!;
    public DbSet<SupplierRecurringRule> SupplierRecurringRules { get; set; } = null!;

    // Stripe schema
    public DbSet<StripeCustomer> StripeCustomers { get; set; } = null!;
    public DbSet<WebhookEvent> WebhookEvents { get; set; } = null!;
    public DbSet<StripeConnectedAccount> StripeConnectedAccounts { get; set; } = null!;
    public DbSet<StripeCheckoutSession> StripeCheckoutSessions { get; set; } = null!;
    public DbSet<BusinessApiKey> BusinessApiKeys { get; set; } = null!;

    // Platform configuration
    public DbSet<PlatformConfig> PlatformConfigs { get; set; } = null!;

    // Promo code schema
    public DbSet<PromoCode> PromoCodes { get; set; } = null!;
    public DbSet<PromoCodeRedemption> PromoCodeRedemptions { get; set; } = null!;

    // Demo invitation schema
    public DbSet<DemoInvitation> DemoInvitations { get; set; } = null!;
    public DbSet<DemoInvitationPermission> DemoInvitationPermissions { get; set; } = null!;

    // Reminder schema
    public DbSet<PaymentReminderSchedule> PaymentReminderSchedules { get; set; } = null!;
    public DbSet<PaymentReminderLog> PaymentReminderLogs { get; set; } = null!;

    // Cashflow schema
    public DbSet<CashFlowSettings> CashFlowSettings { get; set; } = null!;

    // Document schema
    public DbSet<DocumentAttachment> DocumentAttachments { get; set; } = null!;

    // Signature & Receipt schema
    public DbSet<Signature> Signatures { get; set; } = null!;
    public DbSet<PaymentReceipt> PaymentReceipts { get; set; } = null!;
    public DbSet<PaymentReceiptLine> PaymentReceiptLines { get; set; } = null!;
    public DbSet<PaymentReceiptShare> PaymentReceiptShares { get; set; } = null!;

    // Import schema
    public DbSet<Entities.Import.ParserTemplate> ParserTemplates { get; set; } = null!;
    public DbSet<Entities.Import.SupplierImportProfile> SupplierImportProfiles { get; set; } = null!;
    public DbSet<Entities.Import.ImportSession> ImportSessions { get; set; } = null!;

    // Revenue ingestion schema
    public DbSet<RevenueSource> RevenueSources { get; set; } = null!;
    public DbSet<RevenueSummary> RevenueSummaries { get; set; } = null!;
    public DbSet<RevenueSummaryLine> RevenueSummaryLines { get; set; } = null!;
    public DbSet<ExternalSalesRecord> ExternalSalesRecords { get; set; } = null!;

    // Sales pipeline schema
    public DbSet<SalesProduct> SalesProducts { get; set; } = null!;
    public DbSet<SalesContact> SalesContacts { get; set; } = null!;
    public DbSet<LeadRequest> LeadRequests { get; set; } = null!;
    public DbSet<LeadResponse> LeadResponses { get; set; } = null!;
    public DbSet<LeadResponseTemplate> LeadResponseTemplates { get; set; } = null!;
    public DbSet<Meeting> Meetings { get; set; } = null!;
    public DbSet<MeetingProductRequest> MeetingProductRequests { get; set; } = null!;
    public DbSet<MeetingOpportunity> MeetingOpportunities { get; set; } = null!;
    public DbSet<TeamMember> TeamMembers { get; set; } = null!;
    public DbSet<ActivityFeedEntry> ActivityFeedEntries { get; set; } = null!;
    public DbSet<Entities.Sales.LeadSourceType> LeadSourceTypes { get; set; } = null!;
    public DbSet<LeadSourceReferenceType> LeadSourceReferenceTypes { get; set; } = null!;
    public DbSet<Entities.Sales.LeadStatusType> LeadStatusTypes { get; set; } = null!;
    public DbSet<LeadPriorityType> LeadPriorityTypes { get; set; } = null!;
    public DbSet<Entities.Sales.LeadResponseType> LeadResponseTypes { get; set; } = null!;
    public DbSet<Entities.Sales.MeetingType> MeetingTypes { get; set; } = null!;
    public DbSet<FollowUpTask> FollowUpTasks { get; set; } = null!;

    // Compliance schema
    public DbSet<ApplicationCategory> ApplicationCategories { get; set; } = null!;
    public DbSet<ApplicationType> ApplicationTypes { get; set; } = null!;
    public DbSet<BusinessApplication> BusinessApplications { get; set; } = null!;
    public DbSet<ApplicationAttachment> ApplicationAttachments { get; set; } = null!;

    // What's New Announcements
    public DbSet<FeatureAnnouncement> FeatureAnnouncements { get; set; } = null!;
    public DbSet<UserAnnouncementDismissal> UserAnnouncementDismissals { get; set; } = null!;

    // Payroll
    public DbSet<PayslipStatusType> PayslipStatusTypes { get; set; } = null!;
    public DbSet<DeductionCategoryType> DeductionCategoryTypes { get; set; } = null!;
    public DbSet<SalaryType> SalaryTypes { get; set; } = null!;
    public DbSet<Department> Departments { get; set; } = null!;
    public DbSet<Employee> Employees { get; set; } = null!;
    public DbSet<EarningType> EarningTypes { get; set; } = null!;
    public DbSet<DeductionType> DeductionTypes { get; set; } = null!;
    public DbSet<DeductionRateHistory> DeductionRateHistories { get; set; } = null!;
    public DbSet<EmployeeDefaultEarnings> EmployeeDefaultEarnings { get; set; } = null!;
    public DbSet<PayslipPeriod> PayslipPeriods { get; set; } = null!;
    public DbSet<Payslip> Payslips { get; set; } = null!;
    public DbSet<PayslipEarningLine> PayslipEarningLines { get; set; } = null!;
    public DbSet<PayslipDeductionLine> PayslipDeductionLines { get; set; } = null!;
    public DbSet<PayslipEmailLog> PayslipEmailLogs { get; set; } = null!;
    public DbSet<PayslipAuditLog> PayslipAuditLogs { get; set; } = null!;
    public DbSet<PayslipAuditActionType> PayslipAuditActionTypes { get; set; } = null!;

    // Payroll Phase D
    public DbSet<PayeTaxBand> PayeTaxBands { get; set; } = null!;
    public DbSet<CountryDeductionTemplate> CountryDeductionTemplates { get; set; } = null!;
    public DbSet<PayslipPeriodComplianceFiling> PayslipPeriodComplianceFilings { get; set; } = null!;

    // Payment Schedule Overview (keyless — for read-only query results)
    public DbSet<ScheduleOverviewRawRow> ScheduleOverviewRawRows { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureBusiness(modelBuilder);
        ConfigureBusinessProfile(modelBuilder);
        ConfigureBusinessPaymentDetail(modelBuilder);
        ConfigureCustomer(modelBuilder);
        ConfigureQuotationStatusType(modelBuilder);
        ConfigureQuotation(modelBuilder);
        ConfigureQuotationLine(modelBuilder);
        ConfigureInvoiceStatusType(modelBuilder);
        ConfigureInvoiceFinancialStatusType(modelBuilder);
        ConfigureInvoice(modelBuilder);
        ConfigureInvoiceLine(modelBuilder);
        ConfigureInvoiceShare(modelBuilder);
        ConfigureInvoiceAcceptance(modelBuilder);
        ConfigurePaymentMethodType(modelBuilder);
        ConfigurePayment(modelBuilder);
        ConfigureSupplier(modelBuilder);
        ConfigureExpenseCategory(modelBuilder);
        ConfigurePurchaseOriginType(modelBuilder);
        ConfigureExpenseType(modelBuilder);
        ConfigurePurchaseType(modelBuilder);
        ConfigurePurchase(modelBuilder);
        ConfigureExpenseCategoryLimit(modelBuilder);
        ConfigureVatSubmissionPeriod(modelBuilder);
        ConfigureVatSubmission(modelBuilder);
        ConfigureAuditLog(modelBuilder);
        ConfigureProposalShare(modelBuilder);
        ConfigureProposalAcceptance(modelBuilder);
        ConfigureProposalSection(modelBuilder);
        ConfigureBusinessLogo(modelBuilder);
        ConfigureQuotationContact(modelBuilder);
        ConfigureLineItemCatalog(modelBuilder);
        ConfigureProduct(modelBuilder);
        ConfigureProductPriceHistory(modelBuilder);
        ConfigureProductPriceTier(modelBuilder);
        ConfigureCreditNoteStatusType(modelBuilder);
        ConfigureCreditNote(modelBuilder);
        ConfigureCreditNoteLine(modelBuilder);
        ConfigureCreditNoteApplication(modelBuilder);
        ConfigurePlan(modelBuilder);
        ConfigurePlanFeature(modelBuilder);
        ConfigureBusinessPlan(modelBuilder);
        ConfigureSubscription(modelBuilder);
        ConfigureBillingInvoice(modelBuilder);
        ConfigureBillingPayment(modelBuilder);
        ConfigureInvoiceSequence(modelBuilder);
        ConfigureSupplierRecurringRule(modelBuilder);
        ConfigureStripeCustomer(modelBuilder);
        ConfigureWebhookEvent(modelBuilder);
        ConfigureStripeConnectedAccount(modelBuilder);
        ConfigureStripeCheckoutSession(modelBuilder);
        ConfigureBusinessApiKey(modelBuilder);
        ConfigurePromoCode(modelBuilder);
        ConfigurePromoCodeRedemption(modelBuilder);
        ConfigurePlatformConfig(modelBuilder);
        ConfigureDemoInvitation(modelBuilder);
        ConfigureDemoInvitationPermission(modelBuilder);
        ConfigurePaymentReminderSchedule(modelBuilder);
        ConfigurePaymentReminderLog(modelBuilder);
        ConfigureCashFlowSettings(modelBuilder);
        ConfigurePaymentSchedule(modelBuilder);
        ConfigurePaymentScheduleInstalment(modelBuilder);
        ConfigurePaymentScheduleInstalmentStatusType(modelBuilder);
        ConfigurePaymentScheduleHistory(modelBuilder);
        ConfigureScheduleOverviewRawRow(modelBuilder);
        ConfigureDocumentAttachment(modelBuilder);
        ConfigureSignature(modelBuilder);
        ConfigureExpenseCategoryTemplate(modelBuilder);
        ConfigurePaymentReceipt(modelBuilder);
        ConfigurePaymentReceiptLine(modelBuilder);
        ConfigurePaymentReceiptShare(modelBuilder);
        ConfigureParserTemplate(modelBuilder);
        ConfigureSupplierImportProfile(modelBuilder);
        ConfigureImportSession(modelBuilder);
        ConfigureRevenueSource(modelBuilder);
        ConfigureRevenueSummary(modelBuilder);
        ConfigureRevenueSummaryLine(modelBuilder);
        ConfigureExternalSalesRecord(modelBuilder);

        // Sales pipeline
        ConfigureSalesProduct(modelBuilder);
        ConfigureSalesContact(modelBuilder);
        ConfigureLeadSourceType(modelBuilder);
        ConfigureLeadSourceReferenceType(modelBuilder);
        ConfigureLeadStatusType(modelBuilder);
        ConfigureLeadPriorityType(modelBuilder);
        ConfigureLeadResponseType(modelBuilder);
        ConfigureMeetingType(modelBuilder);
        ConfigureLeadRequest(modelBuilder);
        ConfigureLeadResponseTemplate(modelBuilder);
        ConfigureLeadResponse(modelBuilder);
        ConfigureMeeting(modelBuilder);
        ConfigureMeetingProductRequest(modelBuilder);
        ConfigureMeetingOpportunity(modelBuilder);
        ConfigureTeamMember(modelBuilder);
        ConfigureFollowUpTask(modelBuilder);
        ConfigureActivityFeed(modelBuilder);

        // Compliance
        ConfigureApplicationCategory(modelBuilder);
        ConfigureApplicationType(modelBuilder);
        ConfigureBusinessApplication(modelBuilder);
        ConfigureApplicationAttachment(modelBuilder);

        // What's New Announcements
        ConfigureFeatureAnnouncement(modelBuilder);
        ConfigureUserAnnouncementDismissal(modelBuilder);

        // Payroll entities
        ConfigurePayslipStatusType(modelBuilder);
        ConfigureDeductionCategoryType(modelBuilder);
        ConfigureSalaryType(modelBuilder);
        ConfigureDepartment(modelBuilder);
        ConfigureEmployee(modelBuilder);
        ConfigureEarningType(modelBuilder);
        ConfigureDeductionType(modelBuilder);
        ConfigureDeductionRateHistory(modelBuilder);
        ConfigureEmployeeDefaultEarnings(modelBuilder);
        ConfigurePayslipPeriod(modelBuilder);
        ConfigurePayslip(modelBuilder);
        ConfigurePayslipEarningLine(modelBuilder);
        ConfigurePayslipDeductionLine(modelBuilder);
        ConfigurePayslipEmailLog(modelBuilder);
        ConfigurePayslipAuditLog(modelBuilder);
        ConfigurePayslipAuditActionType(modelBuilder);

        // Payroll Phase D entities
        ConfigurePayeTaxBand(modelBuilder);
        ConfigureCountryDeductionTemplate(modelBuilder);
        ConfigurePayslipPeriodComplianceFiling(modelBuilder);

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

            entity.Property(e => e.IsDemoAccount)
                .IsRequired()
                .HasDefaultValue(false);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.UpdatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.IsPaymentInstructionsEnabled)
                .IsRequired()
                .HasDefaultValue(false);

            entity.Property(e => e.IsAutoReceiptEnabled)
                .IsRequired()
                .HasDefaultValue(false);

            entity.Property(e => e.IsAutoInvoiceSignatureEnabled)
                .IsRequired()
                .HasDefaultValue(false);

            entity.Property(e => e.IsOnboardingDismissed)
                .IsRequired()
                .HasDefaultValue(false);
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

            entity.Property(e => e.IsZReportEnabled)
                .IsRequired()
                .HasDefaultValue(false);
        });
    }

    private static void ConfigureBusinessPaymentDetail(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BusinessPaymentDetail>(entity =>
        {
            entity.ToTable("BusinessPaymentDetail", "portal");

            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Business)
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.Property(e => e.Label)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.BankName)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Iban)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.PayeeName)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.SwiftBic)
                .HasMaxLength(11)
                .IsRequired(false);
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

            entity.Property(e => e.IsReminderOptedOut)
                .IsRequired()
                .HasDefaultValue(false);

            entity.Property(e => e.ContactId).IsRequired(false);
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

            entity.Property(e => e.LeadRequestId).IsRequired(false);

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

            entity.Property(e => e.ProductCode)
                .HasMaxLength(50);
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

            entity.Property(e => e.IsDisputed)
                .IsRequired()
                .HasDefaultValue(false);

            entity.Property(e => e.PaymentInstructionsOverride)
                .IsRequired(false);

            entity.Property(e => e.LeadRequestId).IsRequired(false);
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

            entity.Property(e => e.ProductCode)
                .HasMaxLength(50);
        });
    }

    private static void ConfigureInvoiceShare(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InvoiceShare>(entity =>
        {
            entity.ToTable("InvoiceShare", "invoice");

            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Invoice)
                .WithMany()
                .HasForeignKey(e => e.InvoiceId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.Business)
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(e => e.InvoiceId)
                .HasDatabaseName("IX_InvoiceShare_InvoiceId");

            entity.HasIndex(e => e.BusinessId)
                .HasDatabaseName("IX_InvoiceShare_BusinessId");

            entity.HasIndex(e => e.ShareToken)
                .IsUnique()
                .HasDatabaseName("UX_InvoiceShare_ShareToken");

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

    private static void ConfigureInvoiceAcceptance(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InvoiceAcceptance>(entity =>
        {
            entity.ToTable("InvoiceAcceptance", "invoice");

            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.InvoiceShare)
                .WithOne()
                .HasForeignKey<InvoiceAcceptance>(e => e.InvoiceShareId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(e => e.InvoiceShareId)
                .IsUnique()
                .HasDatabaseName("UX_InvoiceAcceptance_InvoiceShareId");

            entity.Property(e => e.AcceptedTerms)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.IpAddress)
                .IsRequired()
                .HasMaxLength(45);

            entity.Property(e => e.UserAgent)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("SYSDATETIMEOFFSET()");
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

            // InvoiceId is nullable — NULL for parent (global) payments
            entity.HasOne(e => e.Invoice)
                .WithMany(i => i.Payments)
                .HasForeignKey(e => e.InvoiceId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.PaymentMethodType)
                .WithMany(p => p.Payments)
                .HasForeignKey(e => e.PaymentMethodTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            // Self-referencing FK: ParentPaymentId → Payment.Id
            entity.HasOne(e => e.ParentPayment)
                .WithMany(e => e.ChildAllocations)
                .HasForeignKey(e => e.ParentPaymentId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.ClientSetNull);

            // CustomerId FK → Customer.Id (set on parent payments only)
            entity.HasOne(e => e.Customer)
                .WithMany()
                .HasForeignKey(e => e.CustomerId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(e => e.BusinessId)
                .HasDatabaseName("IX_Payment_BusinessId");

            entity.HasIndex(e => e.ParentPaymentId)
                .HasDatabaseName("IX_Payment_ParentPaymentId")
                .HasFilter("[ParentPaymentId] IS NOT NULL");

            entity.HasIndex(e => e.CustomerId)
                .HasDatabaseName("IX_Payment_CustomerId")
                .HasFilter("[CustomerId] IS NOT NULL");

            entity.Property(e => e.Amount)
                .HasPrecision(18, 2);

            entity.Property(e => e.CreditAmount)
                .HasPrecision(18, 2)
                .HasDefaultValue(0m);

            entity.Property(e => e.IsAutoAllocated)
                .IsRequired()
                .HasDefaultValue(false);

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

            entity.Property(e => e.IsSystemGenerated).IsRequired().HasDefaultValue(false);
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

            entity.HasOne(e => e.ExpenseType)
                .WithMany()
                .HasForeignKey(e => e.ExpenseTypeId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(e => e.BusinessId)
                .HasDatabaseName("IX_ExpenseCategory_BusinessId");

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    private static void ConfigurePurchaseOriginType(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PurchaseOriginType>(entity =>
        {
            entity.ToTable("PurchaseOriginType", "purchase");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasData(
                new PurchaseOriginType { Id = 1, Name = "Domestic" },
                new PurchaseOriginType { Id = 2, Name = "EuReverseCharge" },
                new PurchaseOriginType { Id = 3, Name = "NonEu" },
                new PurchaseOriginType { Id = 4, Name = "EuPaid" }
            );
        });
    }

    private static void ConfigureExpenseType(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExpenseType>(entity =>
        {
            entity.ToTable("ExpenseType", "purchase");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasData(
                new ExpenseType { Id = 1, Name = "Services" },
                new ExpenseType { Id = 2, Name = "Goods" }
            );
        });
    }

    private static void ConfigurePurchaseType(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PurchaseType>(entity =>
        {
            entity.ToTable("PurchaseType", "purchase");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasData(
                new PurchaseType { Id = 1, Name = "Asset" },
                new PurchaseType { Id = 2, Name = "Stock" },
                new PurchaseType { Id = 3, Name = "Expense" }
            );
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

            entity.HasOne(e => e.PurchaseOriginType)
                .WithMany()
                .HasForeignKey(e => e.PurchaseOriginTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.PurchaseType)
                .WithMany()
                .HasForeignKey(e => e.PurchaseTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.VatSubmissionPeriod)
                .WithMany()
                .HasForeignKey(e => e.VatSubmissionPeriodId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .IsRequired(false);

            entity.HasIndex(e => e.BusinessId)
                .HasDatabaseName("IX_Purchase_BusinessId");

            entity.Property(e => e.InvoiceNumber)
                .HasMaxLength(100);

            entity.Property(e => e.Description)
                .IsRequired(false)
                .HasMaxLength(500);

            entity.Property(e => e.AmountExcludingVat)
                .HasPrecision(18, 2);

            entity.Property(e => e.VatAmount)
                .HasPrecision(18, 2);

            entity.Property(e => e.TotalAmount)
                .HasPrecision(18, 2);

            entity.Property(e => e.PurchaseOriginTypeId)
                .IsRequired()
                .HasDefaultValue(1);

            entity.Property(e => e.PurchaseTypeId)
                .IsRequired()
                .HasDefaultValue(3);

            entity.Property(e => e.Country)
                .HasMaxLength(100);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.UpdatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.PayslipPeriodId).IsRequired(false);
            entity.Property(e => e.CancelledByUserId).HasMaxLength(450).IsRequired(false);
        });
    }

    private static void ConfigureExpenseCategoryLimit(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExpenseCategoryLimit>(entity =>
        {
            entity.ToTable("ExpenseCategoryLimit", "purchase");

            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Business)
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.ExpenseCategory)
                .WithMany()
                .HasForeignKey(e => e.ExpenseCategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(e => e.BusinessId)
                .HasDatabaseName("IX_ExpenseCategoryLimit_BusinessId");

            entity.HasIndex(e => new { e.BusinessId, e.ExpenseCategoryId })
                .IsUnique()
                .HasDatabaseName("UX_ExpenseCategoryLimit_BusinessId_ExpenseCategoryId");

            entity.Property(e => e.AnnualLimitEur)
                .HasPrecision(18, 2);

            entity.Property(e => e.PeriodLimitEur)
                .HasPrecision(18, 2);

            entity.Property(e => e.CreatedAtUtc)
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

    private static void ConfigureProposalAcceptance(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProposalAcceptance>(entity =>
        {
            entity.ToTable("ProposalAcceptance", "quotation");

            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.ProposalShare)
                .WithOne()
                .HasForeignKey<ProposalAcceptance>(e => e.ProposalShareId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(e => e.ProposalShareId)
                .IsUnique()
                .HasDatabaseName("UX_ProposalAcceptance_ProposalShareId");

            entity.Property(e => e.AcceptedTerms)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.IpAddress)
                .IsRequired()
                .HasMaxLength(45);

            entity.Property(e => e.UserAgent)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("SYSDATETIMEOFFSET()");
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

    private static void ConfigureProduct(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Product", "product");

            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Business)
                .WithMany(b => b.Products)
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.Supplier)
                .WithMany(s => s.Products)
                .HasForeignKey(e => e.SupplierId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.BusinessId)
                .HasDatabaseName("IX_Product_BusinessId");

            entity.HasIndex(e => new { e.BusinessId, e.ProductCode })
                .IsUnique()
                .HasDatabaseName("UQ_Product_BusinessId_ProductCode");

            entity.Property(e => e.ProductCode)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.DefaultSellingPrice)
                .HasColumnType("decimal(18,2)");

            entity.Property(e => e.DefaultCostPrice)
                .HasColumnType("decimal(18,2)");

            entity.Property(e => e.DefaultVatRate)
                .HasColumnType("decimal(5,2)");

            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            entity.ToTable(t => t.HasCheckConstraint(
                "CK_Product_DefaultSellingPrice",
                "[DefaultSellingPrice] >= 0"));

            entity.ToTable(t => t.HasCheckConstraint(
                "CK_Product_DefaultCostPrice",
                "[DefaultCostPrice] >= 0"));

            entity.ToTable(t => t.HasCheckConstraint(
                "CK_Product_DefaultVatRate",
                "[DefaultVatRate] >= 0.00 AND [DefaultVatRate] <= 99.99"));
        });
    }

    private static void ConfigureProductPriceHistory(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProductPriceHistory>(entity =>
        {
            entity.ToTable("ProductPriceHistory", "product");

            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Product)
                .WithMany(p => p.PriceHistory)
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.ProductId)
                .HasDatabaseName("IX_ProductPriceHistory_ProductId");

            entity.Property(e => e.SellingPrice)
                .HasColumnType("decimal(18,2)");

            entity.Property(e => e.CostPrice)
                .HasColumnType("decimal(18,2)");

            entity.Property(e => e.EffectiveFromUtc)
                .IsRequired();

            entity.Property(e => e.ChangedByUserId)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    private static void ConfigureProductPriceTier(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProductPriceTier>(entity =>
        {
            entity.ToTable("ProductPriceTier", "product");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.TierName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.SellingPrice).HasPrecision(18, 2);
            entity.Property(e => e.CostPrice).HasPrecision(18, 2);
            entity.Property(e => e.IsDefault).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(e => new { e.ProductId, e.TierName })
                .HasFilter("[IsActive] = 1")
                .IsUnique()
                .HasDatabaseName("UQ_ProductPriceTier_ActiveName");
        });
    }

    private static void ConfigureCreditNoteStatusType(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CreditNoteStatusType>(entity =>
        {
            entity.ToTable("CreditNoteStatusType", "credit");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.HasData(
                new CreditNoteStatusType { Id = 1, Name = "Draft" },
                new CreditNoteStatusType { Id = 2, Name = "Issued" },
                new CreditNoteStatusType { Id = 3, Name = "Applied" },
                new CreditNoteStatusType { Id = 4, Name = "Voided" }
            );
        });
    }

    private static void ConfigureCreditNote(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CreditNote>(entity =>
        {
            entity.ToTable("CreditNote", "credit");
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Business).WithMany()
                .HasForeignKey(e => e.BusinessId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(e => e.Invoice).WithMany()
                .HasForeignKey(e => e.InvoiceId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(e => e.Customer).WithMany()
                .HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(e => e.CreditNoteStatusType).WithMany(s => s.CreditNotes)
                .HasForeignKey(e => e.CreditNoteStatusTypeId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(e => e.VatSubmissionPeriod).WithMany()
                .HasForeignKey(e => e.VatSubmissionPeriodId).OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(e => e.BusinessId).HasDatabaseName("IX_CreditNote_BusinessId");
            entity.HasIndex(e => e.InvoiceId).HasDatabaseName("IX_CreditNote_InvoiceId");
            entity.HasIndex(e => new { e.BusinessId, e.CreditNoteNumber })
                .IsUnique()
                .HasDatabaseName("UX_CreditNote_BusinessId_CreditNoteNumber")
                .HasFilter("[CreditNoteStatusTypeId] <> 4");

            entity.Property(e => e.CreditNoteNumber).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Reason).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.Subtotal).HasPrecision(18, 2);
            entity.Property(e => e.TaxAmount).HasPrecision(18, 2);
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
            entity.Property(e => e.CreatedByUserId).HasMaxLength(450);
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");
        });
    }

    private static void ConfigureCreditNoteLine(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CreditNoteLine>(entity =>
        {
            entity.ToTable("CreditNoteLine", "credit");
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.CreditNote).WithMany(cn => cn.CreditNoteLines)
                .HasForeignKey(e => e.CreditNoteId).OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Quantity).HasPrecision(18, 4);
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
            entity.Property(e => e.VatRate).HasPrecision(5, 2);
            entity.Property(e => e.LineTotal).HasPrecision(18, 2);
        });
    }

    private static void ConfigureCreditNoteApplication(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CreditNoteApplication>(entity =>
        {
            entity.ToTable("CreditNoteApplication", "credit");
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.CreditNote).WithMany(cn => cn.CreditNoteApplications)
                .HasForeignKey(e => e.CreditNoteId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(e => e.Invoice).WithMany()
                .HasForeignKey(e => e.InvoiceId).OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(e => e.CreditNoteId)
                .HasDatabaseName("IX_CreditNoteApplication_CreditNoteId");
            entity.HasIndex(e => e.InvoiceId)
                .HasDatabaseName("IX_CreditNoteApplication_InvoiceId");

            entity.Property(e => e.AmountApplied).HasPrecision(18, 2);
            entity.Property(e => e.IsVoided).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.AppliedByUserId).HasMaxLength(450);
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");
        });
    }

    private static void ConfigurePlan(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Plan>(entity =>
        {
            entity.ToTable("Plan", "dbo");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Slug).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.Slug).IsUnique().HasDatabaseName("UX_Plan_Slug");

            entity.Property(e => e.MonthlyPriceEur).HasPrecision(10, 2);
            entity.Property(e => e.AnnualPriceEur).HasPrecision(10, 2);
            entity.Property(e => e.MaxUsers).IsRequired();
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.DisplayOrder).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.StripeProductId).HasMaxLength(100);
            entity.Property(e => e.StripePriceId).HasMaxLength(100);

            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");

            entity.ToTable(t => t.HasCheckConstraint("CK_Plan_MonthlyPriceEur", "[MonthlyPriceEur] >= 0.00"));
            entity.ToTable(t => t.HasCheckConstraint("CK_Plan_AnnualPriceEur", "[AnnualPriceEur] IS NULL OR [AnnualPriceEur] >= 0.00"));
            entity.ToTable(t => t.HasCheckConstraint("CK_Plan_MaxUsers", "[MaxUsers] = -1 OR [MaxUsers] >= 1"));
        });
    }

    private static void ConfigurePlanFeature(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlanFeature>(entity =>
        {
            entity.ToTable("PlanFeature", "dbo");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ModuleName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.IsIncluded).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.AccessLevel).IsRequired().HasMaxLength(20).HasDefaultValueSql("'full'");
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Plan)
                .WithMany(p => p.PlanFeatures)
                .HasForeignKey(e => e.PlanId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(e => new { e.PlanId, e.ModuleName })
                .IsUnique()
                .HasDatabaseName("UX_PlanFeature_PlanId_ModuleName");

            entity.HasIndex(e => e.PlanId).HasDatabaseName("IX_PlanFeature_PlanId");
        });
    }

    private static void ConfigureBusinessPlan(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BusinessPlan>(entity =>
        {
            entity.ToTable("BusinessPlan", "dbo");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.StartDateUtc).IsRequired();
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20).HasDefaultValueSql("'active'");
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Business)
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Plan)
                .WithMany(p => p.BusinessPlans)
                .HasForeignKey(e => e.PlanId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(e => new { e.BusinessId, e.IsActive })
                .IsUnique()
                .HasDatabaseName("UX_BusinessPlan_BusinessId_IsActive")
                .HasFilter("[IsActive] = 1");

            entity.HasIndex(e => e.BusinessId).HasDatabaseName("IX_BusinessPlan_BusinessId");
            entity.HasIndex(e => e.PlanId).HasDatabaseName("IX_BusinessPlan_PlanId");
        });
    }

    private static void ConfigureSubscription(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.ToTable("Subscription", "billing");
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Business)
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.Plan)
                .WithMany()
                .HasForeignKey(e => e.PlanId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(e => e.BusinessId)
                .HasDatabaseName("IX_Subscription_BusinessId");

            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(20);

            entity.ToTable(t => t.HasCheckConstraint(
                "CK_Subscription_Status",
                "[Status] IN ('active','past_due','cancelled','trialing','incomplete','unpaid')"));

            entity.Property(e => e.StripeSubscriptionId)
                .HasMaxLength(100);

            entity.Property(e => e.CurrentPeriodStart).IsRequired();
            entity.Property(e => e.CurrentPeriodEnd).IsRequired();

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    private static void ConfigureBillingInvoice(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BillingInvoice>(entity =>
        {
            entity.ToTable("Invoice", "billing");
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Business)
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(e => e.BusinessId)
                .HasDatabaseName("IX_BillingInvoice_BusinessId");

            entity.Property(e => e.StripeInvoiceId)
                .HasMaxLength(100);

            entity.Property(e => e.AmountEur)
                .HasPrecision(10, 2);

            entity.ToTable(t => t.HasCheckConstraint(
                "CK_BillingInvoice_AmountEur",
                "[AmountEur] >= 0.00"));

            entity.Property(e => e.PeriodStart).IsRequired();
            entity.Property(e => e.PeriodEnd).IsRequired();

            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(20);

            entity.ToTable(t => t.HasCheckConstraint(
                "CK_BillingInvoice_Status",
                "[Status] IN ('draft','open','paid','void','uncollectible')"));

            entity.Property(e => e.InvoiceNumber)
                .HasMaxLength(50);

            entity.HasIndex(e => e.InvoiceNumber)
                .IsUnique()
                .HasDatabaseName("UX_Invoice_InvoiceNumber")
                .HasFilter("[InvoiceNumber] IS NOT NULL");

            entity.Property(e => e.IsEmailSent)
                .IsRequired()
                .HasDefaultValue(false);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    private static void ConfigureBillingPayment(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BillingPayment>(entity =>
        {
            entity.ToTable("Payment", "billing");
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.BillingInvoice)
                .WithMany(i => i.BillingPayments)
                .HasForeignKey(e => e.InvoiceId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(e => e.InvoiceId)
                .HasDatabaseName("IX_BillingPayment_InvoiceId");

            entity.Property(e => e.AmountEur)
                .HasPrecision(10, 2);

            entity.ToTable(t => t.HasCheckConstraint(
                "CK_BillingPayment_AmountEur",
                "[AmountEur] >= 0.00"));

            entity.Property(e => e.Method)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.PaidAtUtc).IsRequired();

            entity.Property(e => e.StripePaymentIntentId)
                .HasMaxLength(100);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    private static void ConfigureInvoiceSequence(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InvoiceSequence>(entity =>
        {
            entity.ToTable("InvoiceSequence", "billing");

            entity.HasKey(e => e.Year);

            entity.Property(e => e.Year)
                .ValueGeneratedNever();

            entity.Property(e => e.LastNumber)
                .IsRequired()
                .HasDefaultValue(0);

            entity.ToTable(t => t.HasCheckConstraint(
                "CK_InvoiceSequence_LastNumber",
                "[LastNumber] >= 0"));

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    private static void ConfigureSupplierRecurringRule(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SupplierRecurringRule>(entity =>
        {
            entity.ToTable("SupplierRecurringRule", "purchase");

            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Business)
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.Supplier)
                .WithMany()
                .HasForeignKey(e => e.SupplierId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.ExpenseCategory)
                .WithMany()
                .HasForeignKey(e => e.ExpenseCategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .IsRequired(false);

            entity.HasIndex(e => new { e.BusinessId, e.SupplierId })
                .HasDatabaseName("IX_SupplierRecurringRule_BusinessId_SupplierId");

            entity.HasIndex(e => e.BusinessId)
                .HasDatabaseName("IX_SupplierRecurringRule_BusinessId");

            entity.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.ExpectedAmount)
                .HasPrecision(18, 2);

            entity.Property(e => e.AmountTolerancePercent)
                .HasPrecision(5, 2)
                .HasDefaultValue(5.00m);

            entity.Property(e => e.GracePeriodDays)
                .IsRequired()
                .HasDefaultValue(0);

            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(e => e.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    private static void ConfigureStripeCustomer(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StripeCustomer>(entity =>
        {
            entity.ToTable("Customer", "stripe");
            entity.HasKey(e => e.Id);

            entity.HasOne<Business>()
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(e => e.BusinessId)
                .HasDatabaseName("IX_StripeCustomer_BusinessId");

            entity.Property(e => e.StripeCustomerId)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasIndex(e => e.StripeCustomerId)
                .IsUnique()
                .HasDatabaseName("UX_StripeCustomer_StripeCustomerId");

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    private static void ConfigureWebhookEvent(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WebhookEvent>(entity =>
        {
            entity.ToTable("WebhookEvent", "stripe");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.EventId)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasIndex(e => e.EventId)
                .IsUnique()
                .HasDatabaseName("UX_WebhookEvent_EventId");

            entity.Property(e => e.Type)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    private static void ConfigureStripeConnectedAccount(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StripeConnectedAccount>(entity =>
        {
            entity.ToTable("ConnectedAccount", "stripe");
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Business)
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(e => e.BusinessId)
                .IsUnique()
                .HasDatabaseName("UX_ConnectedAccount_BusinessId");

            entity.Property(e => e.StripeAccountId)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(e => e.ConnectedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    private static void ConfigureStripeCheckoutSession(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StripeCheckoutSession>(entity =>
        {
            entity.ToTable("CheckoutSession", "stripe");
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Business)
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.Invoice)
                .WithMany()
                .HasForeignKey(e => e.InvoiceId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.Property(e => e.StripeSessionId)
                .IsRequired()
                .HasMaxLength(255);

            entity.HasIndex(e => e.StripeSessionId)
                .IsUnique()
                .HasDatabaseName("UX_CheckoutSession_StripeSessionId");

            entity.Property(e => e.Amount)
                .IsRequired()
                .HasColumnType("decimal(18,2)");

            entity.Property(e => e.StripeFeeAmount)
                .HasColumnType("decimal(18,2)");

            entity.Property(e => e.NetAmount)
                .HasColumnType("decimal(18,2)");

            entity.Property(e => e.Currency)
                .IsRequired()
                .HasMaxLength(3)
                .HasDefaultValue("EUR");

            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("pending");

            entity.Property(e => e.StripePaymentIntentId)
                .HasMaxLength(255);

            entity.Property(e => e.StripeChargeId)
                .HasMaxLength(255);

            entity.Property(e => e.CustomerName)
                .HasMaxLength(255);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.BusinessId)
                .HasDatabaseName("IX_CheckoutSession_BusinessId");

            entity.HasIndex(e => e.InvoiceId)
                .HasDatabaseName("IX_CheckoutSession_InvoiceId");
        });
    }

    private static void ConfigureBusinessApiKey(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BusinessApiKey>(entity =>
        {
            entity.ToTable("BusinessApiKeys", "stripe");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.BusinessId).IsRequired();
            entity.Property(e => e.KeyType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.EncryptedValue).IsRequired();
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => new { e.BusinessId, e.KeyType }).IsUnique();

            entity.HasOne(e => e.Business)
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.Restrict);
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

    private static void ConfigurePromoCode(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PromoCode>(entity =>
        {
            entity.ToTable("PromoCode", "dbo");

            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.Code)
                .IsUnique()
                .HasDatabaseName("UX_PromoCode_Code");

            entity.Property(e => e.Code)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.DurationMonths)
                .IsRequired();

            // CHECK constraint: DurationMonths BETWEEN 1 AND 24
            // EF Core does not support CHECK constraints directly; enforced at database level via migration

            entity.Property(e => e.MaxRedemptions)
                .IsRequired();

            entity.Property(e => e.CurrentRedemptions)
                .IsRequired()
                .HasDefaultValue(0);

            // CHECK constraint: CurrentRedemptions >= 0 AND CurrentRedemptions <= MaxRedemptions
            // Enforced at database level via migration

            entity.Property(e => e.ExpiresAtUtc)
                .IsRequired();

            entity.Property(e => e.BoundEmail)
                .HasMaxLength(256);

            entity.Property(e => e.IsRevoked)
                .IsRequired()
                .HasDefaultValue(false);

            entity.Property(e => e.CreatedByUserId)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            entity.HasMany(e => e.Redemptions)
                .WithOne(r => r.PromoCode)
                .HasForeignKey(r => r.PromoCodeId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });
    }

    private static void ConfigurePromoCodeRedemption(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PromoCodeRedemption>(entity =>
        {
            entity.ToTable("PromoCodeRedemption", "dbo");

            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.PromoCode)
                .WithMany(p => p.Redemptions)
                .HasForeignKey(e => e.PromoCodeId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.Business)
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(e => e.PromoCodeId)
                .HasDatabaseName("IX_PromoCodeRedemption_PromoCodeId");

            entity.HasIndex(e => e.BusinessId)
                .HasDatabaseName("IX_PromoCodeRedemption_BusinessId");

            entity.Property(e => e.UserId)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(e => e.RedeemedAtUtc)
                .IsRequired();

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    private static void ConfigurePlatformConfig(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlatformConfig>(entity =>
        {
            entity.ToTable("PlatformConfig", "dbo");

            entity.HasKey(e => e.Key);

            entity.Property(e => e.Key)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(e => e.Value)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasMaxLength(500);

            entity.Property(e => e.LastModifiedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    private static void ConfigureDemoInvitation(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DemoInvitation>(entity =>
        {
            entity.ToTable("DemoInvitation", "portal");

            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Business)
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasMany(e => e.Permissions)
                .WithOne(p => p.DemoInvitation)
                .HasForeignKey(p => p.DemoInvitationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.Token)
                .IsUnique()
                .HasDatabaseName("UX_DemoInvitation_Token");

            entity.HasIndex(e => e.Status)
                .HasDatabaseName("IX_DemoInvitation_Status");

            entity.ToTable(t => t.HasCheckConstraint(
                "CK_DemoInvitation_Status",
                "[Status] IN ('sent', 'accessed', 'expired', 'revoked')"));

            entity.Property(e => e.Token)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.RecipientEmail)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(e => e.RecipientName)
                .HasMaxLength(200);

            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(e => e.CreatedByUserId)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(e => e.AccessCount)
                .IsRequired()
                .HasDefaultValue(0);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    private static void ConfigureDemoInvitationPermission(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DemoInvitationPermission>(entity =>
        {
            entity.ToTable("DemoInvitationPermission", "portal");

            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.DemoInvitation)
                .WithMany(d => d.Permissions)
                .HasForeignKey(e => e.DemoInvitationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.DemoInvitationId, e.Module })
                .IsUnique()
                .HasDatabaseName("UQ_DemoInvitationPermission_Module");

            entity.ToTable(t => t.HasCheckConstraint(
                "CK_DemoInvitationPermission_Module",
                "[Module] IN ('customer', 'quotation', 'invoice', 'revenue', 'purchase', 'vat', 'credit', 'audit', 'products', 'payment_link_manual', 'payment_reminder_manual', 'payment_link_auto', 'payment_reminder_auto', 'cashflow', 'pnl', 'expense_insights', 'attachments', 'client_portal', 'activity_timeline', 'audit_log', 'api', 'webhooks', 'multi_currency', 'schedule_payments', 'recurring_expense_validation', 'purchase_import', 'zreport_import', 'sales')"));

            entity.ToTable(t => t.HasCheckConstraint(
                "CK_DemoInvitationPermission_AccessLevel",
                "[AccessLevel] IN ('full', 'readonly', 'none')"));

            entity.Property(e => e.Module)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.AccessLevel)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    private static void ConfigurePaymentReminderSchedule(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PaymentReminderSchedule>(entity =>
        {
            entity.ToTable("PaymentReminderSchedule", "reminder");
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Business)
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(e => e.BusinessId)
                .HasDatabaseName("IX_PaymentReminderSchedule_BusinessId");

            entity.Property(e => e.EscalationTier)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(e => e.MaxRemindersPerTier)
                .IsRequired()
                .HasDefaultValue(1);

            entity.Property(e => e.MinIntervalDays)
                .IsRequired()
                .HasDefaultValue(3);

            entity.Property(e => e.PartialPaymentSuppressionDays)
                .IsRequired()
                .HasDefaultValue(7);

            entity.Property(e => e.IsEnabled)
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

    private static void ConfigurePaymentReminderLog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PaymentReminderLog>(entity =>
        {
            entity.ToTable("PaymentReminderLog", "reminder");
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Business)
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.Invoice)
                .WithMany()
                .HasForeignKey(e => e.InvoiceId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.Customer)
                .WithMany()
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(e => new { e.BusinessId, e.InvoiceId })
                .HasDatabaseName("IX_PaymentReminderLog_BusinessId_InvoiceId");

            entity.HasIndex(e => new { e.BusinessId, e.SentAtUtc })
                .HasDatabaseName("IX_PaymentReminderLog_BusinessId_SentAtUtc");

            entity.Property(e => e.RecipientEmail)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.EscalationTier)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(e => e.ErrorMessage)
                .HasMaxLength(1000);

            entity.Property(e => e.IsManualTrigger)
                .IsRequired()
                .HasDefaultValue(false);

            entity.Property(e => e.SentAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            // Open Tracking columns
            entity.Property(e => e.TrackingToken)
                .HasMaxLength(64);

            entity.HasIndex(e => e.TrackingToken)
                .IsUnique()
                .HasFilter("[TrackingToken] IS NOT NULL")
                .HasDatabaseName("UX_PaymentReminderLog_TrackingToken");

            entity.Property(e => e.IsOpened)
                .IsRequired()
                .HasDefaultValue(false);

            entity.Property(e => e.OpenedAtUtc);

            entity.Property(e => e.OpenCount)
                .IsRequired()
                .HasDefaultValue(0);

            entity.Property(e => e.LastOpenedAtUtc);

            // Test Send flag
            entity.Property(e => e.IsTestSend)
                .IsRequired()
                .HasDefaultValue(false);

            // Filtered index for queries excluding test sends
            entity.HasIndex(e => new { e.BusinessId, e.InvoiceId, e.EscalationTier })
                .HasFilter("[IsTestSend] = 0")
                .HasDatabaseName("IX_PaymentReminderLog_BusinessId_IsTestSend");
        });
    }

    private static void ConfigureCashFlowSettings(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CashFlowSettings>(entity =>
        {
            entity.ToTable("CashFlowSettings", "cashflow");

            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Business)
                .WithOne()
                .HasForeignKey<CashFlowSettings>(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(e => e.BusinessId)
                .IsUnique()
                .HasDatabaseName("UQ_CashFlowSettings_BusinessId");

            entity.Property(e => e.StartingBalance)
                .IsRequired()
                .HasColumnType("decimal(18,2)")
                .HasDefaultValue(0m);

            entity.Property(e => e.AlertThreshold)
                .IsRequired()
                .HasColumnType("decimal(18,2)")
                .HasDefaultValue(0m);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.UpdatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            entity.ToTable(t => t.HasCheckConstraint(
                "CK_CashFlowSettings_StartingBalance", "[StartingBalance] >= 0"));

            entity.ToTable(t => t.HasCheckConstraint(
                "CK_CashFlowSettings_AlertThreshold", "[AlertThreshold] >= 0"));
        });
    }

    private static void ConfigurePaymentSchedule(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PaymentSchedule>(entity =>
        {
            entity.ToTable("PaymentSchedule", "revenue");
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Business)
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.Invoice)
                .WithMany()
                .HasForeignKey(e => e.InvoiceId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(e => e.InvoiceId)
                .IsUnique()
                .HasDatabaseName("UX_PaymentSchedule_InvoiceId_Active")
                .HasFilter("[IsActive] = 1");

            entity.HasIndex(e => e.BusinessId)
                .HasDatabaseName("IX_PaymentSchedule_BusinessId");

            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.CreatedByUserId)
                .HasMaxLength(450);
        });
    }

    private static void ConfigurePaymentScheduleInstalment(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PaymentScheduleInstalment>(entity =>
        {
            entity.ToTable("PaymentScheduleInstalment", "revenue");
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.PaymentSchedule)
                .WithMany(s => s.Instalments)
                .HasForeignKey(e => e.PaymentScheduleId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.Payment)
                .WithMany()
                .HasForeignKey(e => e.PaymentId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.ParentInstalment)
                .WithMany()
                .HasForeignKey(e => e.ParentInstalmentId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(e => e.PaymentScheduleId)
                .HasDatabaseName("IX_PSInstalment_PaymentScheduleId");

            entity.Property(e => e.Amount)
                .HasPrecision(18, 2);

            entity.Property(e => e.MatchedAmount)
                .HasPrecision(18, 2)
                .HasDefaultValue(0m);

            entity.Property(e => e.IsRemainder)
                .IsRequired()
                .HasDefaultValue(false);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    private static void ConfigurePaymentScheduleInstalmentStatusType(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PaymentScheduleInstalmentStatusType>(entity =>
        {
            entity.ToTable("PaymentScheduleInstalmentStatusType", "revenue");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasData(
                new PaymentScheduleInstalmentStatusType { Id = 1, Name = "Pending" },
                new PaymentScheduleInstalmentStatusType { Id = 2, Name = "Due" },
                new PaymentScheduleInstalmentStatusType { Id = 3, Name = "Overdue" },
                new PaymentScheduleInstalmentStatusType { Id = 4, Name = "Paid" },
                new PaymentScheduleInstalmentStatusType { Id = 5, Name = "PartiallyPaid" }
            );
        });
    }

    private static void ConfigurePaymentScheduleHistory(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PaymentScheduleHistory>(entity =>
        {
            entity.ToTable("PaymentScheduleHistory", "revenue");
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.PaymentSchedule)
                .WithMany(s => s.History)
                .HasForeignKey(e => e.PaymentScheduleId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(e => e.PaymentScheduleId)
                .HasDatabaseName("IX_PSHistory_PaymentScheduleId");

            entity.Property(e => e.FieldChanged)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.OldValue)
                .HasMaxLength(500);

            entity.Property(e => e.NewValue)
                .HasMaxLength(500);

            entity.Property(e => e.ChangedByUserId)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(e => e.ChangedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    private static void ConfigureScheduleOverviewRawRow(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ScheduleOverviewRawRow>(entity =>
        {
            entity.HasNoKey();
            entity.ToView(null); // Not mapped to any table — used for raw SQL queries only
        });
    }

    private static void ConfigureDocumentAttachment(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DocumentAttachment>(entity =>
        {
            entity.ToTable("DocumentAttachment", "document");

            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Business)
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.Property(e => e.EntityType)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.FileName)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.OriginalFileName)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.ContentType)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.StoragePath)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.FileSizeBytes)
                .IsRequired();

            entity.Property(e => e.UploadedByUserId)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(e => e.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            entity.Property(e => e.DeletedAtUtc)
                .IsRequired(false);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => new { e.BusinessId, e.EntityType, e.EntityId })
                .HasDatabaseName("IX_DocumentAttachment_BusinessId_EntityType_EntityId")
                .HasFilter("[IsDeleted] = 0");
        });
    }

    private static void ConfigureParserTemplate(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Entities.Import.ParserTemplate>(entity =>
        {
            entity.ToTable("ParserTemplate", "import");

            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Business)
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.Supplier)
                .WithMany()
                .HasForeignKey(e => e.SupplierId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.FileFormatType)
                .IsRequired()
                .HasMaxLength(10);

            entity.Property(e => e.SheetName)
                .HasMaxLength(100);

            entity.Property(e => e.ColumnMappingsJson)
                .IsRequired();

            entity.Property(e => e.IsManaged)
                .IsRequired()
                .HasDefaultValue(false);

            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.UpdatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => new { e.BusinessId, e.SupplierId })
                .HasDatabaseName("IX_ParserTemplate_BusinessId_SupplierId")
                .HasFilter("[IsActive] = 1");
        });
    }

    private static void ConfigureSupplierImportProfile(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Entities.Import.SupplierImportProfile>(entity =>
        {
            entity.ToTable("SupplierImportProfile", "import");

            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Business)
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.Supplier)
                .WithMany()
                .HasForeignKey(e => e.SupplierId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.DefaultExpenseCategory)
                .WithMany()
                .HasForeignKey(e => e.DefaultExpenseCategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.DefaultPurchaseOriginType)
                .WithMany()
                .HasForeignKey(e => e.DefaultPurchaseOriginTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.Property(e => e.DefaultCountry)
                .HasMaxLength(100);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.UpdatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => new { e.BusinessId, e.SupplierId })
                .IsUnique()
                .HasDatabaseName("UQ_SupplierImportProfile_Business_Supplier");
        });
    }

    private static void ConfigureImportSession(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Entities.Import.ImportSession>(entity =>
        {
            entity.ToTable("ImportSession", "import");

            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Business)
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.Supplier)
                .WithMany()
                .HasForeignKey(e => e.SupplierId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.ParserTemplate)
                .WithMany()
                .HasForeignKey(e => e.ParserTemplateId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.Property(e => e.FileName)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.RowDataJson)
                .IsRequired();

            entity.Property(e => e.IsConfirmed)
                .IsRequired()
                .HasDefaultValue(false);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    private static void ConfigureExpenseCategoryTemplate(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExpenseCategoryTemplate>(entity =>
        {
            entity.ToTable("ExpenseCategoryTemplate", "purchase");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");
        });
    }

    private static void ConfigureSignature(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Signature>(entity =>
        {
            entity.ToTable("Signature", "portal");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Label).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Position).HasMaxLength(100);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ContentType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.FilePath).IsRequired().HasMaxLength(500);
            entity.Property(e => e.IsDefault).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.UploadedByUserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne<Business>()
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });
    }

    private static void ConfigurePaymentReceipt(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PaymentReceipt>(entity =>
        {
            entity.ToTable("PaymentReceipt", "revenue");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ReceiptNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.TotalAmountReceived).HasPrecision(18, 2);
            entity.Property(e => e.OutstandingBalanceAfter).HasPrecision(18, 2);
            entity.Property(e => e.PaymentReference).HasMaxLength(200);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.IsVoided).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.CreatedByUserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => new { e.BusinessId, e.ReceiptNumber }).IsUnique();
            entity.HasIndex(e => e.PaymentId);
            entity.HasIndex(e => new { e.BusinessId, e.CustomerId });

            entity.HasOne<Business>()
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne<Payment>()
                .WithMany()
                .HasForeignKey(e => e.PaymentId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne<PaymentMethodType>()
                .WithMany()
                .HasForeignKey(e => e.PaymentMethodTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne<Signature>()
                .WithMany()
                .HasForeignKey(e => e.SignatureId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasMany(e => e.Lines)
                .WithOne()
                .HasForeignKey(e => e.PaymentReceiptId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigurePaymentReceiptLine(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PaymentReceiptLine>(entity =>
        {
            entity.ToTable("PaymentReceiptLine", "revenue");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.InvoiceNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.InvoiceTotal).HasPrecision(18, 2);
            entity.Property(e => e.InvoiceOutstandingBefore).HasPrecision(18, 2);
            entity.Property(e => e.InvoiceOutstandingAfter).HasPrecision(18, 2);

            entity.HasIndex(e => e.PaymentReceiptId);

            entity.HasOne<Payment>()
                .WithMany()
                .HasForeignKey(e => e.PaymentId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne<Invoice>()
                .WithMany()
                .HasForeignKey(e => e.InvoiceId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });
    }

    private static void ConfigurePaymentReceiptShare(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PaymentReceiptShare>(entity =>
        {
            entity.ToTable("PaymentReceiptShare", "revenue");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ShareToken).IsRequired().HasMaxLength(100);
            entity.Property(e => e.SnapshotHtml).IsRequired();
            entity.Property(e => e.CustomerEmail).IsRequired().HasMaxLength(200);
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.CreatedByUserId).IsRequired().HasMaxLength(450);

            entity.HasIndex(e => e.ShareToken).IsUnique();
            entity.HasIndex(e => e.PaymentReceiptId).HasFilter("[IsActive] = 1");

            entity.HasOne<PaymentReceipt>()
                .WithMany()
                .HasForeignKey(e => e.PaymentReceiptId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne<Business>()
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });
    }

    private static void ConfigureRevenueSource(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RevenueSource>(entity =>
        {
            entity.ToTable("RevenueSource", "revenue");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Description)
                .HasMaxLength(500);

            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Business)
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });
    }

    private static void ConfigureRevenueSummary(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RevenueSummary>(entity =>
        {
            entity.ToTable("RevenueSummary", "revenue");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.SummaryDate).IsRequired();
            entity.Property(e => e.ZReportNumber).HasMaxLength(50);
            entity.Property(e => e.TotalNet).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.TotalVat).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.TotalGross).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.TotalDiscount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Reference).HasMaxLength(200);
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Business)
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.RevenueSource)
                .WithMany(s => s.RevenueSummaries)
                .HasForeignKey(e => e.RevenueSourceId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.VatSubmissionPeriod)
                .WithMany()
                .HasForeignKey(e => e.VatSubmissionPeriodId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });
    }

    private static void ConfigureRevenueSummaryLine(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RevenueSummaryLine>(entity =>
        {
            entity.ToTable("RevenueSummaryLine", "revenue");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.VatRate).HasColumnType("decimal(5,2)").IsRequired();
            entity.Property(e => e.NetAmount).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.VatAmount).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Description).HasMaxLength(200);
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.RevenueSummary)
                .WithMany(s => s.Lines)
                .HasForeignKey(e => e.RevenueSummaryId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureExternalSalesRecord(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExternalSalesRecord>(entity =>
        {
            entity.ToTable("ExternalSalesRecord", "revenue");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.TransactionDate).IsRequired();
            entity.Property(e => e.InvoiceNumber).HasMaxLength(100);
            entity.Property(e => e.NetAmount).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.VatAmount).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.PaymentMethod).HasMaxLength(50);
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Business)
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.RevenueSource)
                .WithMany()
                .HasForeignKey(e => e.RevenueSourceId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.Customer)
                .WithMany()
                .HasForeignKey(e => e.CustomerId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.VatSubmissionPeriod)
                .WithMany()
                .HasForeignKey(e => e.VatSubmissionPeriodId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });
    }

    // ═══════════════════════════════════════════════════════════
    // SALES PIPELINE SCHEMA
    // ═══════════════════════════════════════════════════════════

    private static void ConfigureSalesProduct(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SalesProduct>(entity =>
        {
            entity.ToTable("Product", "sales");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Business)
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });
    }

    private static void ConfigureSalesContact(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SalesContact>(entity =>
        {
            entity.ToTable("Contact", "sales");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(320);
            entity.Property(e => e.PhoneNumber).HasMaxLength(30);
            entity.Property(e => e.CompanyName).HasMaxLength(200);
            entity.Property(e => e.JobTitle).HasMaxLength(100);
            entity.Property(e => e.Country).HasMaxLength(100);
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Business)
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(e => new { e.BusinessId, e.Email })
                .IsUnique()
                .HasDatabaseName("UX_SalesContact_BusinessId_Email")
                .HasFilter("[Email] IS NOT NULL");

            entity.HasIndex(e => new { e.BusinessId, e.PhoneNumber })
                .IsUnique()
                .HasDatabaseName("UX_SalesContact_BusinessId_PhoneNumber")
                .HasFilter("[PhoneNumber] IS NOT NULL");
        });
    }

    private static void ConfigureLeadSourceType(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Entities.Sales.LeadSourceType>(entity =>
        {
            entity.ToTable("LeadSourceType", "sales");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
        });
    }

    private static void ConfigureLeadSourceReferenceType(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LeadSourceReferenceType>(entity =>
        {
            entity.ToTable("LeadSourceReferenceType", "sales");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
        });
    }

    private static void ConfigureLeadStatusType(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Entities.Sales.LeadStatusType>(entity =>
        {
            entity.ToTable("LeadStatusType", "sales");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.Property(e => e.DisplayOrder).IsRequired();
            entity.Property(e => e.Colour).HasMaxLength(7);
            entity.Property(e => e.IsTerminal).IsRequired().HasDefaultValue(false);
        });
    }

    private static void ConfigureLeadResponseType(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Entities.Sales.LeadResponseType>(entity =>
        {
            entity.ToTable("LeadResponseType", "sales");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
        });
    }

    private static void ConfigureLeadPriorityType(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LeadPriorityType>(entity =>
        {
            entity.ToTable("LeadPriorityType", "sales");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.Property(e => e.DisplayOrder).IsRequired();
            entity.Property(e => e.Colour).IsRequired().HasMaxLength(10);
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");
        });
    }

    private static void ConfigureMeetingType(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Entities.Sales.MeetingType>(entity =>
        {
            entity.ToTable("MeetingType", "sales");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
        });
    }

    private static void ConfigureLeadRequest(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LeadRequest>(entity =>
        {
            entity.ToTable("LeadRequest", "sales");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.SourceUrl).HasMaxLength(500);
            entity.Property(e => e.AssignedToUserId).HasMaxLength(450);
            entity.Property(e => e.IsCancelled).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.CancellationDescription).HasMaxLength(500);
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.LeadStatusTypeId).IsRequired().HasDefaultValue(1);
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Business)
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.Contact)
                .WithMany(c => c.LeadRequests)
                .HasForeignKey(e => e.ContactId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.Product)
                .WithMany(p => p.LeadRequests)
                .HasForeignKey(e => e.ProductId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.LeadSourceType)
                .WithMany()
                .HasForeignKey(e => e.LeadSourceTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.LeadSourceReferenceType)
                .WithMany()
                .HasForeignKey(e => e.LeadSourceReferenceTypeId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.LeadStatusType)
                .WithMany()
                .HasForeignKey(e => e.LeadStatusTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.LeadPriorityType)
                .WithMany()
                .HasForeignKey(e => e.LeadPriorityTypeId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });
    }

    private static void ConfigureLeadResponseTemplate(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LeadResponseTemplate>(entity =>
        {
            entity.ToTable("LeadResponseTemplate", "sales");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Subject).HasMaxLength(300);
            entity.Property(e => e.BodyTemplate).IsRequired();
            entity.Property(e => e.ResponseTimeInHours).IsRequired();
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Business)
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.Product)
                .WithMany(p => p.Templates)
                .HasForeignKey(e => e.ProductId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.LeadResponseType)
                .WithMany()
                .HasForeignKey(e => e.LeadResponseTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });
    }

    private static void ConfigureLeadResponse(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LeadResponse>(entity =>
        {
            entity.ToTable("LeadResponse", "sales");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.RespondedByUserId).HasMaxLength(450);
            entity.Property(e => e.IsAutomated).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.SentAtUtc).IsRequired();
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.LeadRequest)
                .WithMany(lr => lr.Responses)
                .HasForeignKey(e => e.LeadRequestId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.LeadResponseType)
                .WithMany()
                .HasForeignKey(e => e.LeadResponseTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.LeadResponseTemplate)
                .WithMany()
                .HasForeignKey(e => e.LeadResponseTemplateId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });
    }

    private static void ConfigureMeeting(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Meeting>(entity =>
        {
            entity.ToTable("Meeting", "sales");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Subject).IsRequired().HasMaxLength(300);
            entity.Property(e => e.ScheduledAtUtc).IsRequired();
            entity.Property(e => e.DurationMinutes).IsRequired().HasDefaultValue(60);
            entity.Property(e => e.Location).HasMaxLength(300);
            entity.Property(e => e.IsCancelled).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.CancellationDescription).HasMaxLength(500);
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.CreatedByUserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Business)
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.LeadRequest)
                .WithMany(lr => lr.Meetings)
                .HasForeignKey(e => e.LeadRequestId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Contact)
                .WithMany(c => c.Meetings)
                .HasForeignKey(e => e.ContactId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.MeetingType)
                .WithMany()
                .HasForeignKey(e => e.MeetingTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });
    }

    private static void ConfigureMeetingProductRequest(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MeetingProductRequest>(entity =>
        {
            entity.ToTable("MeetingProductRequest", "sales");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.IsCancelled).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.CancellationDescription).HasMaxLength(500);
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Meeting)
                .WithMany(m => m.ProductRequests)
                .HasForeignKey(e => e.MeetingId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.Product)
                .WithMany(p => p.MeetingProductRequests)
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });
    }

    private static void ConfigureMeetingOpportunity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MeetingOpportunity>(entity =>
        {
            entity.ToTable("MeetingOpportunity", "sales");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Title).IsRequired().HasMaxLength(300);
            entity.Property(e => e.EstimatedValue).HasColumnType("decimal(18,2)");
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Meeting)
                .WithMany(m => m.Opportunities)
                .HasForeignKey(e => e.MeetingId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });
    }

    private static void ConfigureTeamMember(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TeamMember>(entity =>
        {
            entity.ToTable("TeamMember", "sales");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(200);
            entity.Property(e => e.PhoneNumber).HasMaxLength(50);
            entity.Property(e => e.Role).HasMaxLength(100);
            entity.Property(e => e.UserId).HasMaxLength(450);
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Business)
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(e => e.BusinessId).HasDatabaseName("IX_TeamMember_BusinessId");
        });
    }

    private static void ConfigureFollowUpTask(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FollowUpTask>(entity =>
        {
            entity.ToTable("FollowUpTask", "sales");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.TaskType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.DueAtUtc).IsRequired();
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.IsCompleted).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.SnoozedCount).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.TaskOutcome).HasMaxLength(20);
            entity.Property(e => e.ScheduledTimeUtc).HasColumnType("time(0)");
            entity.Property(e => e.CreatedByUserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Business)
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.LeadRequest)
                .WithMany()
                .HasForeignKey(e => e.LeadRequestId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Contact)
                .WithMany()
                .HasForeignKey(e => e.ContactId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.TeamMember)
                .WithMany()
                .HasForeignKey(e => e.TeamMemberId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }

    private static void ConfigureActivityFeed(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ActivityFeedEntry>(entity =>
        {
            entity.ToTable("ActivityFeed", "sales");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Action).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
            entity.Property(e => e.PerformedByUserId).HasMaxLength(450);
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Business)
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.LeadRequest)
                .WithMany()
                .HasForeignKey(e => e.LeadRequestId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.PerformedByTeamMember)
                .WithMany()
                .HasForeignKey(e => e.PerformedByTeamMemberId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .IsRequired(false);

            entity.HasIndex(e => new { e.LeadRequestId, e.CreatedAtUtc })
                .HasDatabaseName("IX_ActivityFeed_LeadRequestId_CreatedAtUtc")
                .IsDescending(false, true);

            entity.HasIndex(e => e.BusinessId).HasDatabaseName("IX_ActivityFeed_BusinessId");
        });
    }

    private static void ConfigureApplicationCategory(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationCategory>(entity =>
        {
            entity.ToTable("ApplicationCategory", "compliance");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Description)
                .HasMaxLength(500);

            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.Name)
                .IsUnique()
                .HasDatabaseName("UQ_ApplicationCategory_Name");
        });
    }

    private static void ConfigureApplicationType(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationType>(entity =>
        {
            entity.ToTable("ApplicationType", "compliance");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Description)
                .HasMaxLength(1000);

            entity.Property(e => e.Country)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Frequency)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(e => e.EstimatedAmount)
                .HasColumnType("decimal(18,2)");

            entity.Property(e => e.FrequencyInterval);

            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne<ApplicationCategory>()
                .WithMany()
                .HasForeignKey(e => e.ApplicationCategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(e => new { e.Name, e.Country })
                .IsUnique()
                .HasDatabaseName("UQ_ApplicationType_NameCountry");
        });
    }

    private static void ConfigureBusinessApplication(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BusinessApplication>(entity =>
        {
            entity.ToTable("BusinessApplication", "compliance");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.BusinessId)
                .IsRequired();

            entity.Property(e => e.ApplicationTypeId)
                .IsRequired();

            entity.Property(e => e.DueDate)
                .IsRequired();

            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("Pending");

            entity.Property(e => e.ReferenceNumber)
                .HasMaxLength(100);

            entity.Property(e => e.Notes)
                .HasMaxLength(2000);

            entity.Property(e => e.EstimatedAmount)
                .HasColumnType("decimal(18,2)");

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne<ApplicationType>()
                .WithMany()
                .HasForeignKey(e => e.ApplicationTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(e => new { e.BusinessId, e.DueDate })
                .HasDatabaseName("IX_BusinessApplication_BusinessId_DueDate");

            entity.HasIndex(e => new { e.BusinessId, e.Status })
                .HasDatabaseName("IX_BusinessApplication_BusinessId_Status");
        });
    }

    private static void ConfigureApplicationAttachment(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationAttachment>(entity =>
        {
            entity.ToTable("ApplicationAttachment", "compliance");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.BusinessApplicationId)
                .IsRequired();

            entity.Property(e => e.FileName)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.OriginalFileName)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.FilePath)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.ContentType)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.FileSizeBytes)
                .IsRequired();

            entity.Property(e => e.UploadedByUserId)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne<BusinessApplication>()
                .WithMany()
                .HasForeignKey(e => e.BusinessApplicationId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });
    }

    private static void ConfigureFeatureAnnouncement(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FeatureAnnouncement>(entity =>
        {
            entity.ToTable("FeatureAnnouncements", "dbo");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Summary).IsRequired().HasMaxLength(500);
            entity.Property(e => e.DetailHtml).IsRequired();
            entity.Property(e => e.ModuleKey).HasMaxLength(100);
            entity.Property(e => e.CtaLabel).HasMaxLength(100);
            entity.Property(e => e.CtaUrl).HasMaxLength(500);
            entity.Property(e => e.TargetPlanTier).HasMaxLength(50);
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.PublishedAtUtc).IsRequired();
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");
        });
    }

    private static void ConfigureUserAnnouncementDismissal(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAnnouncementDismissal>(entity =>
        {
            entity.ToTable("UserAnnouncementDismissals", "dbo");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.DismissedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.FeatureAnnouncement)
                .WithMany()
                .HasForeignKey(e => e.FeatureAnnouncementId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(e => new { e.UserId, e.FeatureAnnouncementId })
                .IsUnique()
                .HasDatabaseName("UQ_UserAnnouncementDismissals_UserAnnouncement");
        });
    }

    // ═══════════════════════════════════════════════════════════
    // PAYROLL SCHEMA
    // ═══════════════════════════════════════════════════════════

    private static void ConfigurePayslipStatusType(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PayslipStatusType>(entity =>
        {
            entity.ToTable("PayslipStatusType", "payroll");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(20);
        });
    }

    private static void ConfigureDeductionCategoryType(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DeductionCategoryType>(entity =>
        {
            entity.ToTable("DeductionCategoryType", "payroll");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(20);
        });
    }

    private static void ConfigureSalaryType(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SalaryType>(entity =>
        {
            entity.ToTable("SalaryType", "payroll");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
        });
    }

    private static void ConfigureDepartment(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Department>(entity =>
        {
            entity.ToTable("Department", "payroll");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BusinessId).IsRequired();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");
            entity.HasIndex(e => new { e.BusinessId, e.Name }).IsUnique();
        });
    }

    private static void ConfigureEmployee(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Employee>(entity =>
        {
            entity.ToTable("Employee", "payroll");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BusinessId).IsRequired();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(300);
            entity.Property(e => e.Position).HasMaxLength(200);
            entity.Property(e => e.SocialInsuranceNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.IdNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Phone).HasMaxLength(50);
            entity.Property(e => e.Email).HasMaxLength(200);
            entity.Property(e => e.StartDate).IsRequired();
            entity.Property(e => e.SalaryTypeId).IsRequired();
            entity.Property(e => e.BaseSalary).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(e => e.HourlyRate).HasColumnType("decimal(18,2)");
            entity.Property(e => e.BankAccount).HasMaxLength(100);
            entity.Property(e => e.IsPayeApplicable).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");
            entity.HasOne<Department>().WithMany().HasForeignKey(e => e.DepartmentId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne<SalaryType>().WithMany().HasForeignKey(e => e.SalaryTypeId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasIndex(e => new { e.BusinessId, e.SocialInsuranceNumber }).IsUnique();
            entity.HasIndex(e => new { e.BusinessId, e.IdNumber }).IsUnique();
        });
    }

    private static void ConfigureEarningType(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EarningType>(entity =>
        {
            entity.ToTable("EarningType", "payroll");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");
            entity.HasIndex(e => e.Code).IsUnique();
        });
    }

    private static void ConfigureDeductionType(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DeductionType>(entity =>
        {
            entity.ToTable("DeductionType", "payroll");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
            entity.Property(e => e.DeductionCategoryTypeId).IsRequired();
            entity.Property(e => e.Country).IsRequired().HasMaxLength(50).HasDefaultValue("CY");
            entity.Property(e => e.IsPayeDeductible).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");
            entity.HasOne<DeductionCategoryType>().WithMany().HasForeignKey(e => e.DeductionCategoryTypeId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasIndex(e => new { e.BusinessId, e.Code }).IsUnique();
        });
    }

    private static void ConfigureDeductionRateHistory(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DeductionRateHistory>(entity =>
        {
            entity.ToTable("DeductionRateHistory", "payroll");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DeductionTypeId).IsRequired();
            entity.Property(e => e.Rate).IsRequired().HasColumnType("decimal(6,2)");
            entity.Property(e => e.EffectiveFromUtc).IsRequired();
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");
            entity.HasOne<DeductionType>().WithMany().HasForeignKey(e => e.DeductionTypeId).OnDelete(DeleteBehavior.ClientSetNull);
        });
    }

    private static void ConfigureEmployeeDefaultEarnings(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EmployeeDefaultEarnings>(entity =>
        {
            entity.ToTable("EmployeeDefaultEarnings", "payroll");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EmployeeId).IsRequired();
            entity.Property(e => e.EarningTypeId).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(300);
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.OvertimeMultiplier).HasColumnType("decimal(4,2)");
            entity.Property(e => e.OvertimeHours).HasColumnType("decimal(6,2)");
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");
            entity.HasOne<Employee>().WithMany().HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne<EarningType>().WithMany().HasForeignKey(e => e.EarningTypeId).OnDelete(DeleteBehavior.ClientSetNull);
        });
    }

    private static void ConfigurePayslipPeriod(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PayslipPeriod>(entity =>
        {
            entity.ToTable("PayslipPeriod", "payroll");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BusinessId).IsRequired();
            entity.Property(e => e.Year).IsRequired();
            entity.Property(e => e.Month).IsRequired();
            entity.Property(e => e.PayslipStatusTypeId).IsRequired().HasDefaultValue((byte)1);
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");
            entity.HasOne<PayslipStatusType>().WithMany().HasForeignKey(e => e.PayslipStatusTypeId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasIndex(e => new { e.BusinessId, e.Year, e.Month }).IsUnique();
        });
    }

    private static void ConfigurePayslip(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payslip>(entity =>
        {
            entity.ToTable("Payslip", "payroll");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EmployeeId).IsRequired();
            entity.Property(e => e.PayslipPeriodId).IsRequired();
            entity.Property(e => e.TotalEarnings).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalEmployeeDeductions).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(e => e.NetSalary).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalEmployerContributions).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(e => e.ManagerNotes).HasMaxLength(2000);
            entity.Property(e => e.PayslipStatusTypeId).IsRequired().HasDefaultValue((byte)1);
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");
            entity.HasOne<Employee>().WithMany().HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne<PayslipPeriod>().WithMany().HasForeignKey(e => e.PayslipPeriodId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne<PayslipStatusType>().WithMany().HasForeignKey(e => e.PayslipStatusTypeId).OnDelete(DeleteBehavior.ClientSetNull);
        });
    }

    private static void ConfigurePayslipEarningLine(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PayslipEarningLine>(entity =>
        {
            entity.ToTable("PayslipEarningLine", "payroll");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PayslipId).IsRequired();
            entity.Property(e => e.EarningTypeId).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(300);
            entity.Property(e => e.Amount).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(e => e.OvertimeMultiplier).HasColumnType("decimal(4,2)");
            entity.Property(e => e.OvertimeHours).HasColumnType("decimal(6,2)");
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");
            entity.HasOne<Payslip>().WithMany().HasForeignKey(e => e.PayslipId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne<EarningType>().WithMany().HasForeignKey(e => e.EarningTypeId).OnDelete(DeleteBehavior.ClientSetNull);
        });
    }

    private static void ConfigurePayslipDeductionLine(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PayslipDeductionLine>(entity =>
        {
            entity.ToTable("PayslipDeductionLine", "payroll");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PayslipId).IsRequired();
            entity.Property(e => e.DeductionTypeId).IsRequired();
            entity.Property(e => e.BaseAmount).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(e => e.Rate).IsRequired().HasColumnType("decimal(6,2)");
            entity.Property(e => e.CalculatedAmount).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(e => e.DeductionCategoryTypeId).IsRequired();
            entity.Property(e => e.DeductionRateHistoryId).IsRequired(false);
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");
            entity.HasOne<Payslip>().WithMany().HasForeignKey(e => e.PayslipId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne<DeductionType>().WithMany().HasForeignKey(e => e.DeductionTypeId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne<DeductionRateHistory>().WithMany().HasForeignKey(e => e.DeductionRateHistoryId).IsRequired(false).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne<DeductionCategoryType>().WithMany().HasForeignKey(e => e.DeductionCategoryTypeId).OnDelete(DeleteBehavior.ClientSetNull);
        });
    }

    private static void ConfigurePayslipEmailLog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PayslipEmailLog>(entity =>
        {
            entity.ToTable("PayslipEmailLog", "payroll");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PayslipId).IsRequired();
            entity.Property(e => e.SentByUserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.SentToEmail).IsRequired().HasMaxLength(256);
            entity.Property(e => e.SentAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.IsSuccess).IsRequired();
            entity.Property(e => e.FailureReason).HasMaxLength(500);
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");
            entity.HasOne<Payslip>().WithMany().HasForeignKey(e => e.PayslipId).OnDelete(DeleteBehavior.ClientSetNull);
        });
    }

    private static void ConfigurePayslipAuditLog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PayslipAuditLog>(entity =>
        {
            entity.ToTable("PayslipAuditLog", "payroll");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PayslipId).IsRequired();
            entity.Property(e => e.UserId).HasMaxLength(450).IsRequired();
            entity.Property(e => e.PayslipAuditActionTypeId).IsRequired();
            entity.Property(e => e.FieldName).HasMaxLength(100);
            entity.Property(e => e.OldValue).HasMaxLength(500);
            entity.Property(e => e.NewValue).HasMaxLength(500);
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");
        });
    }

    private static void ConfigurePayslipAuditActionType(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PayslipAuditActionType>(entity =>
        {
            entity.ToTable("PayslipAuditActionType", "payroll");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(20).IsRequired();
        });
    }

    private static void ConfigurePayeTaxBand(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PayeTaxBand>(entity =>
        {
            entity.ToTable("PayeTaxBand", "payroll");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CountryCode).IsRequired().HasMaxLength(3);
            entity.Property(e => e.LowerBound).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(e => e.UpperBound).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Rate).IsRequired().HasColumnType("decimal(5,4)");
            entity.Property(e => e.EffectiveFromYear).IsRequired();
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");
            entity.HasIndex(e => new { e.CountryCode, e.EffectiveFromYear });
        });
    }

    private static void ConfigureCountryDeductionTemplate(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CountryDeductionTemplate>(entity =>
        {
            entity.ToTable("CountryDeductionTemplate", "payroll");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CountryCode).IsRequired().HasMaxLength(3);
            entity.Property(e => e.DeductionName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
            entity.Property(e => e.IsPercentage).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.DeductionCategoryTypeId).IsRequired();
            entity.Property(e => e.DefaultRate).IsRequired().HasColumnType("decimal(5,4)");
            entity.Property(e => e.IsPayeDeductible).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.SortOrder).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");
            entity.HasOne<DeductionCategoryType>().WithMany().HasForeignKey(e => e.DeductionCategoryTypeId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasIndex(e => new { e.CountryCode, e.IsActive });
        });
    }

    private static void ConfigurePayslipPeriodComplianceFiling(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PayslipPeriodComplianceFiling>(entity =>
        {
            entity.ToTable("PayslipPeriodComplianceFiling", "payroll");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PayslipPeriodId).IsRequired();
            entity.Property(e => e.ComplianceFilingId).IsRequired();
            entity.Property(e => e.ContributionTotal).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(e => e.UpdatedAtUtc).IsRequired();
            entity.Property(e => e.UpdatedByUserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");
            entity.HasOne<PayslipPeriod>().WithMany().HasForeignKey(e => e.PayslipPeriodId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne<BusinessApplication>().WithMany().HasForeignKey(e => e.ComplianceFilingId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasIndex(e => e.PayslipPeriodId);
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

        modelBuilder.Entity<ExpenseCategoryLimit>()
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

        modelBuilder.Entity<InvoiceShare>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId);

        modelBuilder.Entity<BusinessLogo>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId);

        modelBuilder.Entity<QuotationContact>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId);

        modelBuilder.Entity<LineItemCatalog>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId);

        modelBuilder.Entity<Product>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId);

        modelBuilder.Entity<PaymentReminderSchedule>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId);

        modelBuilder.Entity<PaymentReminderLog>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId);

        modelBuilder.Entity<CashFlowSettings>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId);

        modelBuilder.Entity<SupplierRecurringRule>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId && !e.IsDeleted);

        modelBuilder.Entity<DocumentAttachment>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId && !e.IsDeleted);

        modelBuilder.Entity<Entities.Import.ParserTemplate>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId && e.IsActive);

        modelBuilder.Entity<Entities.Import.SupplierImportProfile>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId);

        modelBuilder.Entity<Entities.Import.ImportSession>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId);

        modelBuilder.Entity<RevenueSummary>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId && e.IsActive);

        modelBuilder.Entity<RevenueSource>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId);

        modelBuilder.Entity<ExternalSalesRecord>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId);

        modelBuilder.Entity<Signature>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId);

        modelBuilder.Entity<PaymentReceipt>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId);

        modelBuilder.Entity<PaymentReceiptShare>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId);

        modelBuilder.Entity<RevenueSource>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId);

        modelBuilder.Entity<RevenueSummary>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId);

        // Sales pipeline entities
        modelBuilder.Entity<SalesProduct>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId);

        modelBuilder.Entity<SalesContact>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId);

        modelBuilder.Entity<LeadRequest>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId);

        modelBuilder.Entity<LeadResponseTemplate>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId);

        modelBuilder.Entity<Meeting>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId);

        modelBuilder.Entity<FollowUpTask>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId);

        // Compliance entities
        modelBuilder.Entity<BusinessApplication>()
            .HasQueryFilter(e => e.BusinessId == _currentTenantService.CurrentBusinessId);
    }
}
