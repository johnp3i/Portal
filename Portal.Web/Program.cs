using System.Collections.ObjectModel;
using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities.Identity;
using Portal.Infrastructure.Interceptors;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Repositories.Import;
using Portal.Infrastructure.Repositories.Sales;
using Portal.Infrastructure.Services;
using Portal.Infrastructure.Services.Import;
using Portal.Infrastructure.Services.Sales;
using Portal.Web.Extensions;
using Portal.Web.Security;
using Portal.Web.Services;
using Serilog;
using Serilog.Sinks.MSSqlServer;
using Portal.Web.BackgroundServices;
using Portal.Web.Middleware;
using Portal.Web.Services.Billing;
using Portal.Web.Services.Stripe;

var builder = WebApplication.CreateBuilder(args);

// --- Audit Interceptor (scoped to match PortalDbContext lifetime) ---
// AuditInterceptor writes audit records via its own DbContext instance to avoid
// a circular dependency: PortalDbContext → AuditInterceptor → AuditLogRepository → PortalDbContext.
// The interceptor's AuditLogRepository uses a plain DbContext (no interceptors attached).
builder.Services.AddScoped<AuditInterceptor>(sp =>
{
    var tenantService = sp.GetRequiredService<ICurrentTenantService>();
    var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();

    // Build a plain PortalDbContext (no interceptors) for the audit write path
    var connectionString = builder.Configuration.GetConnectionString("PortalDb");
    var auditOptions = new DbContextOptionsBuilder<PortalDbContext>()
        .UseSqlServer(connectionString)
        .Options;
    var auditDbContext = new PortalDbContext(auditOptions, tenantService);
    var auditLogRepository = new AuditLogRepository(auditDbContext);

    return new AuditInterceptor(tenantService, httpContextAccessor, auditLogRepository);
});

// --- Database Contexts ---
builder.Services.AddDbContext<PortalDbContext>((sp, options) =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("PortalDb"));
    options.AddInterceptors(sp.GetRequiredService<AuditInterceptor>());
});

builder.Services.AddDbContext<MembershipDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MembershipDb")));

// --- ASP.NET Core Identity ---
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password policy
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;

    // Lockout policy
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
.AddEntityFrameworkStores<MembershipDbContext>()
.AddDefaultTokenProviders()
.AddClaimsPrincipalFactory<BusinessClaimsPrincipalFactory>();

// --- Cookie Authentication ---
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

// --- Demo Session Cookie (isolated from primary authentication) ---
builder.Services.AddAuthentication().AddCookie("DemoScheme", options =>
{
    options.ExpireTimeSpan = TimeSpan.FromHours(2);
    options.SlidingExpiration = true;
    options.Cookie.Name = ".Portal.Demo";
    options.Events = new Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationEvents
    {
        OnRedirectToLogin = context =>
        {
            // Detect expired demo sessions and redirect to session-expired page
            if (context.Request.Cookies.ContainsKey(".Portal.Demo"))
            {
                context.Response.Redirect("/Demo/SessionExpired");
            }
            else
            {
                context.Response.Redirect(context.RedirectUri);
            }
            return Task.CompletedTask;
        }
    };
});

// --- Application Services ---
builder.Services.AddHttpContextAccessor();
builder.Services.ConfigureWebsiteSettings(builder.Configuration);
builder.Services.ConfigureInvoiceSettings(builder.Configuration);
builder.Services.ConfigureEmailAccounts(builder.Configuration);
builder.Services.ConfigureEmail();

// --- Stripe ---
builder.Services.ConfigureStripe(builder.Configuration);
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddScoped<IWebhookProcessingService, WebhookProcessingService>();
builder.Services.AddScoped<IProvisioningService, ProvisioningService>();
builder.Services.AddScoped<ISetupWizardService, SetupWizardService>();
builder.Services.AddScoped<ISubscriptionPlanService, SubscriptionPlanService>();
builder.Services.AddScoped<IBillingService, BillingService>();
builder.Services.AddScoped<IInvoiceSequenceRepository>(sp =>
    new InvoiceSequenceRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<IInvoiceNumberGenerator, InvoiceNumberGenerator>();
builder.Services.AddScoped<IVatCalculationService, VatCalculationService>();
builder.Services.AddScoped<IInvoiceEmailService, InvoiceEmailService>();
builder.Services.AddScoped<IInvoiceBackfillService, InvoiceBackfillService>();
builder.Services.AddScoped<SubscriptionRepository>(sp =>
    new SubscriptionRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<BillingInvoiceRepository>(sp =>
    new BillingInvoiceRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<BillingPaymentRepository>(sp =>
    new BillingPaymentRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<StripeCustomerRepository>(sp =>
    new StripeCustomerRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<WebhookEventRepository>(sp =>
    new WebhookEventRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<BusinessRepository>(sp =>
    new BusinessRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<CustomerRepository>(sp =>
    new CustomerRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<QuotationRepository>(sp =>
    new QuotationRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<QuotationLineRepository>(sp =>
    new QuotationLineRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<AuditLogRepository>(sp =>
    new AuditLogRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<ProposalShareRepository>(sp =>
    new ProposalShareRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<BusinessLogoRepository>(sp =>
    new BusinessLogoRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<ProposalSectionRepository>(sp =>
    new ProposalSectionRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<QuotationContactRepository>(sp =>
    new QuotationContactRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<LineItemCatalogRepository>(sp =>
    new LineItemCatalogRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<InvoiceRepository>(sp =>
    new InvoiceRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<InvoiceLineRepository>(sp =>
    new InvoiceLineRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<InvoiceSectionRepository>(sp =>
    new InvoiceSectionRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<BusinessPaymentDetailRepository>(sp =>
    new BusinessPaymentDetailRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<SupplierRepository>(sp =>
    new SupplierRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<IBusinessPlanRepository>(sp =>
    new BusinessPlanRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<IPlanRepository>(sp =>
    new PlanRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<IPlanFeatureRepository>(sp =>
    new PlanFeatureRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<PlatformConfigRepository>(sp =>
    new PlatformConfigRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<PromoCodeRepository>(sp =>
    new PromoCodeRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<PromoCodeRedemptionRepository>(sp =>
    new PromoCodeRedemptionRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<IPlatformConfigService, PlatformConfigService>();
builder.Services.AddScoped<IPromoCodeService, PromoCodeService>();
builder.Services.AddScoped<IPromoEmailService, PromoEmailService>();
builder.Services.AddScoped<IPromoCodeValidationService, PromoCodeValidationService>();
builder.Services.AddScoped<ICurrentTenantService, CurrentTenantService>();
builder.Services.AddScoped<IRegistrationService, RegistrationService>();
builder.Services.AddScoped<IPlanService, PlanService>();
builder.Services.AddScoped<IIdentityEmailService, IdentityEmailService>();
builder.Services.AddScoped<IBusinessService, BusinessService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IQuotationService, QuotationService>();
builder.Services.AddScoped<IInvitationService, InvitationService>();
builder.Services.AddScoped<IEmailService, PortalEmailService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IPlanCheckService, PlanCheckService>();
builder.Services.AddScoped<ILogoService, LogoService>();
builder.Services.AddScoped<IViewRenderService, ViewRenderService>();
builder.Services.AddScoped<IProposalRenderer, ProposalRenderer>();
builder.Services.AddScoped<IProposalService, ProposalService>();
builder.Services.AddScoped<ILineItemCatalogService, LineItemCatalogService>();
builder.Services.AddScoped<IProposalSectionService, ProposalSectionService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<IInvoiceSectionService, InvoiceSectionService>();
builder.Services.AddScoped<InvoiceShareRepository>(sp =>
    new InvoiceShareRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<IInvoiceRenderer, InvoiceRenderer>();
builder.Services.AddScoped<IInvoicePdfService, InvoicePdfService>();
builder.Services.AddScoped<IProposalPdfService, ProposalPdfService>();
builder.Services.AddScoped<IInvoiceSharingService, InvoiceSharingService>();
builder.Services.AddScoped<InvoiceAcceptanceRepository>(sp =>
    new InvoiceAcceptanceRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<IInvoiceAcceptanceService, InvoiceAcceptanceService>();
builder.Services.AddScoped<ProposalAcceptanceRepository>(sp =>
    new ProposalAcceptanceRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<IProposalAcceptanceService, ProposalAcceptanceService>();
builder.Services.AddScoped<IDocumentDuplicationService, DocumentDuplicationService>();
builder.Services.AddScoped<IDocumentSoftDeleteService, DocumentSoftDeleteService>();
builder.Services.AddScoped<ISupplierService, SupplierService>();
builder.Services.AddScoped<ISupplierDashboardService, SupplierDashboardService>();
builder.Services.AddScoped<ExpenseCategoryRepository>(sp =>
    new ExpenseCategoryRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<ExpenseCategoryLimitRepository>(sp =>
    new ExpenseCategoryLimitRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<PurchaseRepository>(sp =>
    new PurchaseRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<IPurchaseService, PurchaseService>();
builder.Services.AddScoped<IExpenseCategoryService, ExpenseCategoryService>();
builder.Services.AddScoped<IExpenseCategoryLimitService, ExpenseCategoryLimitService>();
builder.Services.AddScoped<SupplierRecurringRuleRepository>(sp =>
    new SupplierRecurringRuleRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<IRecurringExpenseValidationService, RecurringExpenseValidationService>();
builder.Services.AddScoped<ICsvImportService, CsvImportService>();

// Document Attachments
builder.Services.AddScoped<DocumentAttachmentRepository>(sp =>
    new DocumentAttachmentRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<IDocumentAttachmentService, DocumentAttachmentService>();

// Purchase Import
builder.Services.AddScoped<ParserTemplateRepository>(sp =>
    new ParserTemplateRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<ImportSessionRepository>(sp =>
    new ImportSessionRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<SupplierImportProfileRepository>(sp =>
    new SupplierImportProfileRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<IFileParsingService, FileParsingService>();
builder.Services.AddScoped<IImportValidationService, ImportValidationService>();
builder.Services.AddScoped<IDuplicateDetectionService, DuplicateDetectionService>();
builder.Services.AddScoped<IParserTemplateService, ParserTemplateService>();
builder.Services.AddScoped<IImportEngineService, ImportEngineService>();
builder.Services.AddScoped<VatSubmissionPeriodRepository>(sp =>
    new VatSubmissionPeriodRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<VatSubmissionRepository>(sp =>
    new VatSubmissionRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<IVatPeriodGenerationService, VatPeriodGenerationService>();
builder.Services.AddScoped<IVatSubmissionService, VatSubmissionService>();

// --- Revenue Ingestion (Z-Reports) ---
builder.Services.AddScoped<RevenueSourceRepository>(sp =>
    new RevenueSourceRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<RevenueSummaryRepository>(sp =>
    new RevenueSummaryRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<IRevenueSourceService, RevenueSourceService>();
builder.Services.AddScoped<IRevenueSummaryService, RevenueSummaryService>();
builder.Services.AddScoped<IZReportImportService, ZReportImportService>();
builder.Services.AddScoped<ExternalSalesRecordRepository>(sp =>
    new ExternalSalesRecordRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<ISalesImportService, SalesImportService>();

// --- Sales Pipeline ---
builder.Services.AddScoped<SalesContactRepository>(sp =>
    new SalesContactRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<SalesProductRepository>(sp =>
    new SalesProductRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<LeadRequestRepository>(sp =>
    new LeadRequestRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<LeadResponseRepository>(sp =>
    new LeadResponseRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<LeadResponseTemplateRepository>(sp =>
    new LeadResponseTemplateRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<MeetingRepository>(sp =>
    new MeetingRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<MeetingProductRequestRepository>(sp =>
    new MeetingProductRequestRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<MeetingOpportunityRepository>(sp =>
    new MeetingOpportunityRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<LeadSourceTypeRepository>(sp =>
    new LeadSourceTypeRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<LeadSourceReferenceTypeRepository>(sp =>
    new LeadSourceReferenceTypeRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<LeadStatusTypeRepository>(sp =>
    new LeadStatusTypeRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<LeadPriorityTypeRepository>(sp =>
    new LeadPriorityTypeRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<LeadResponseTypeRepository>(sp =>
    new LeadResponseTypeRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<MeetingTypeRepository>(sp =>
    new MeetingTypeRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<Portal.Infrastructure.Services.Sales.IContactService, Portal.Infrastructure.Services.Sales.ContactService>();
builder.Services.AddScoped<ISalesProductService, SalesProductService>();
builder.Services.AddScoped<ILeadRequestService, LeadRequestService>();
builder.Services.AddScoped<IResponseService, ResponseService>();
builder.Services.AddScoped<IMeetingService, MeetingService>();
builder.Services.AddScoped<ITeamMemberService, TeamMemberService>();
builder.Services.AddScoped<IActivityFeedService, ActivityFeedService>();
builder.Services.AddScoped<IFollowUpTaskService, FollowUpTaskService>();
builder.Services.AddScoped<IInsightsService, InsightsService>();
builder.Services.AddScoped<ITimelineService, TimelineService>();
builder.Services.AddScoped<TeamMemberRepository>(sp =>
    new TeamMemberRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<ActivityFeedRepository>(sp =>
    new ActivityFeedRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<FollowUpTaskRepository>(sp =>
    new FollowUpTaskRepository(sp.GetRequiredService<PortalDbContext>()));

// --- What's New Announcements ---
builder.Services.AddScoped<AnnouncementRepository>(sp =>
    new AnnouncementRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<IAnnouncementService, AnnouncementService>();

// P&L services
builder.Services.AddScoped<IPnlService, PnlService>();
builder.Services.AddScoped<IPnlPdfService, PnlPdfService>();

// Expense Insights
builder.Services.AddScoped<IExpenseInsightsService, ExpenseInsightsService>();

// Payment Reminders
builder.Services.AddScoped<IPaymentReminderScheduleService, PaymentReminderScheduleService>();
builder.Services.AddScoped<IPaymentReminderService, PaymentReminderService>();
builder.Services.AddHostedService<PaymentReminderBackgroundService>();
builder.Services.AddScoped<IPaymentInstructionsService, PaymentInstructionsService>();

// Cash Flow Forecasting
builder.Services.AddScoped<ICashFlowService, CashFlowService>();

// --- reCAPTCHA ---
builder.Services.AddHttpClient<IReCaptchaService, ReCaptchaService>();

// --- Revenue Control ---
builder.Services.AddScoped<PaymentRepository>(sp =>
    new PaymentRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<ICheckoutSessionExpireService, CheckoutSessionExpireService>();
builder.Services.AddScoped<IFinancialStatusEngine>(sp =>
    new FinancialStatusEngine(
        sp.GetRequiredService<PaymentRepository>(),
        sp.GetRequiredService<InvoiceRepository>(),
        sp.GetRequiredService<CreditNoteRepository>(),
        sp.GetRequiredService<ICheckoutSessionExpireService>()));
builder.Services.AddScoped<IPaymentService, PaymentService>();

builder.Services.AddScoped<IPaymentAllocationEngine>(sp =>
    new PaymentAllocationEngine(
        sp.GetRequiredService<PaymentRepository>(),
        sp.GetRequiredService<IFinancialStatusEngine>(),
        sp.GetRequiredService<IPaymentScheduleService>(),
        sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IDashboardBriefingService, DashboardBriefingService>();
builder.Services.AddScoped<ISystemBriefingService, SystemBriefingService>();
builder.Services.AddScoped<IOnboardingService, OnboardingService>();
builder.Services.AddScoped<IReceivablesQueryService, ReceivablesQueryService>();
builder.Services.AddScoped<IVatIntegrationService, VatIntegrationService>();
builder.Services.AddSingleton<IInstalmentStatusEngine>(sp =>
    new InstalmentStatusEngine(TimeProvider.System));
builder.Services.AddSingleton<IInstalmentMatchingEngine, InstalmentMatchingEngine>();
builder.Services.AddScoped<IVatWarningService, VatWarningService>();
builder.Services.AddScoped<PaymentScheduleRepository>(sp =>
    new PaymentScheduleRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<PaymentScheduleInstalmentRepository>(sp =>
    new PaymentScheduleInstalmentRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<PaymentScheduleHistoryRepository>(sp =>
    new PaymentScheduleHistoryRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<IPaymentScheduleService, PaymentScheduleService>();

// --- Payment Schedule Overview ---
builder.Services.AddScoped<PaymentScheduleOverviewRepository>(sp =>
    new PaymentScheduleOverviewRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<IPaymentScheduleOverviewService, PaymentScheduleOverviewService>();

// --- Credit Notes ---
builder.Services.AddScoped<CreditNoteRepository>(sp =>
    new CreditNoteRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<CreditNoteLineRepository>(sp =>
    new CreditNoteLineRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<CreditNoteApplicationRepository>(sp =>
    new CreditNoteApplicationRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<ICreditNoteService, CreditNoteService>();
builder.Services.AddScoped<ICreditNoteRenderer, CreditNoteRenderer>();

// --- Customer Statement ---
builder.Services.AddScoped<StatementRepository>(sp =>
    new StatementRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<IStatementService, StatementService>();
builder.Services.AddScoped<IStatementRenderer, StatementRenderer>();

// --- Payment Receipts & Signatures ---
builder.Services.AddScoped<IProductInsightsService, ProductInsightsService>();
builder.Services.AddScoped<ExpenseCategoryTemplateRepository>(sp =>
    new ExpenseCategoryTemplateRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<IExpenseCategoryTemplateService, ExpenseCategoryTemplateService>();
builder.Services.AddScoped<SignatureRepository>(sp =>
    new SignatureRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<PaymentReceiptRepository>(sp =>
    new PaymentReceiptRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<PaymentReceiptLineRepository>(sp =>
    new PaymentReceiptLineRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<PaymentReceiptShareRepository>(sp =>
    new PaymentReceiptShareRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<ISignatureService, SignatureService>();
builder.Services.AddScoped<IPaymentReceiptService, PaymentReceiptService>();

// --- Product Catalog ---
builder.Services.AddScoped<ProductRepository>(sp =>
    new ProductRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<ProductPriceHistoryRepository>(sp =>
    new ProductPriceHistoryRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<ProductTypeRepository>(sp =>
    new ProductTypeRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IProductAutocompleteService, ProductAutocompleteService>();

// --- Audit & User Admin ---
builder.Services.AddScoped<AuditLogQueryRepository>(sp =>
    new AuditLogQueryRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<IAuditLogQueryService, AuditLogQueryService>();
builder.Services.AddScoped<UserNameResolver>();
builder.Services.AddScoped<IActivitySummaryService, ActivitySummaryService>();
builder.Services.AddScoped<UserAdminRepository>(sp =>
    new UserAdminRepository(sp.GetRequiredService<MembershipDbContext>()));
builder.Services.AddScoped<IUserAdminService, UserAdminService>();

// --- System Logs (read-only, Portal.Logging database) ---
builder.Services.AddDbContext<LoggingDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("LoggingDb"));
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
});
builder.Services.AddScoped<SystemLogQueryRepository>(sp =>
    new SystemLogQueryRepository(sp.GetRequiredService<LoggingDbContext>()));
builder.Services.AddScoped<ISystemLogQueryService, SystemLogQueryService>();

// --- Demo Access Invitations ---
builder.Services.AddScoped<DemoInvitationRepository>(sp =>
    new DemoInvitationRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<IDemoInvitationService, DemoInvitationService>();

// --- Business Insights (SuperAdmin) ---
builder.Services.AddScoped<BusinessInsightsRepository>();
builder.Services.AddScoped<IBusinessInsightsService, BusinessInsightsService>();

// --- User Impersonation (SuperAdmin) ---
builder.Services.AddScoped<Portal.Web.Services.ImpersonationService>();

// --- Stripe Connect (Card Payments) ---
builder.Services.AddScoped<StripeConnectRepository>();
builder.Services.AddScoped<BusinessApiKeysRepository>();
builder.Services.AddScoped<IStripeConnectService, StripeConnectService>();
builder.Services.AddScoped<IStripeKeyEncryptionService, StripeKeyEncryptionService>();
builder.Services.AddScoped<IStripeKeyResolutionService, StripeKeyResolutionService>();

// --- Global Search ---
builder.Services.AddScoped<IGlobalSearchService, GlobalSearchService>();

// --- Payroll ---
builder.Services.Configure<Portal.Infrastructure.Models.PayrollSettings>(builder.Configuration.GetSection("Payroll"));
builder.Services.AddScoped<PayrollRepository>(sp =>
    new PayrollRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<PayslipEmailLogRepository>(sp =>
    new PayslipEmailLogRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<IPayrollService, PayrollService>();
builder.Services.AddSingleton<IPayslipCalculationEngine, PayslipCalculationEngine>();
builder.Services.AddScoped<IPayslipRenderer, PayslipRenderer>();
builder.Services.AddScoped<IPayslipPdfService, PayslipPdfService>();
builder.Services.AddScoped<IPayslipEmailService, PayslipEmailService>();
builder.Services.AddScoped<IPayrollReportService, PayrollReportService>();
builder.Services.AddScoped<IPayslipPeriodStatusService, PayslipPeriodStatusService>();
builder.Services.AddScoped<IPayslipAuditService, PayslipAuditService>();
builder.Services.AddScoped<IPayrollPnlService, PayrollPnlService>();
builder.Services.AddScoped<IPayrollProgressNotifier, Portal.Web.Services.PayrollProgressNotifier>();

// --- Payroll Phase D ---
builder.Services.AddSingleton<IPayeCalculationService, PayeCalculationService>();
builder.Services.AddScoped<IPayslipCalculationOrchestrator, PayslipCalculationOrchestrator>();
builder.Services.AddScoped<IComplianceIntegrationService, ComplianceIntegrationService>();
builder.Services.AddScoped<ICountryTemplateService, CountryTemplateService>();

// --- Compliance Filings ---
builder.Services.AddScoped<ComplianceRepository>(sp =>
    new ComplianceRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<IComplianceService, ComplianceService>();

// --- MVC ---
builder.Services.AddSignalR();
var mvcBuilder = builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<Portal.Web.Filters.SetupWizardRedirectFilter>();
    options.Filters.Add<Portal.Web.Filters.SubscriptionWarningResultFilter>();
    options.Filters.Add<Portal.Web.Filters.DemoPermissionFilter>();
    options.Filters.Add<Portal.Web.Filters.PlanPermissionFilter>();
    options.Filters.Add<Portal.Web.Filters.UserPermissionFilter>();
});

if (builder.Environment.IsDevelopment())
{
    mvcBuilder.AddRazorRuntimeCompilation();
}

// --- Serilog SelfLog (must be before any Serilog configuration to capture config errors) ---
Serilog.Debugging.SelfLog.Enable(msg =>
    File.AppendAllText("logs/serilog-selflog-.txt", $"{DateTime.UtcNow:o} {msg}{Environment.NewLine}"));

// --- Serilog ---
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Portal.Web")
    .Enrich.WithCorrelationId()
    .Enrich.WithMachineName()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File("logs/portal-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{CorrelationId}] [{UserId}] [{BusinessId}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.MSSqlServer(
        connectionString: context.Configuration.GetConnectionString("LoggingDb"),
        sinkOptions: new MSSqlServerSinkOptions
        {
            TableName = "Logs",
            SchemaName = "dbo",
            AutoCreateSqlTable = context.HostingEnvironment.IsDevelopment(),
            BatchPostingLimit = 50,
            BatchPeriod = TimeSpan.FromSeconds(5)
        },
        columnOptions: GetColumnOptions()));

var app = builder.Build();

// --- Seed Data (Development) ---
if (!app.Configuration.GetValue<bool>("SkipSeedData"))
{
    using (var scope = app.Services.CreateScope())
    {
        await Portal.Web.Data.SeedData.InitializeAsync(scope.ServiceProvider);
        await Portal.Web.Data.SeedDemoUser.InitializeAsync(scope.ServiceProvider);
    }
}

// --- Middleware Pipeline ---
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseSerilogRequestLogging();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<LoggingEnrichmentMiddleware>();

app.MapHub<Portal.Web.Hubs.PayrollHub>("/hubs/payroll");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// --- Serilog Column Options Helper ---
static ColumnOptions GetColumnOptions()
{
    var columnOptions = new ColumnOptions();

    // Remove Properties XML column — we use dedicated columns instead
    columnOptions.Store.Remove(StandardColumn.Properties);

    // Add custom columns for structured properties
    columnOptions.AdditionalColumns = new Collection<SqlColumn>
    {
        new SqlColumn { ColumnName = "CorrelationId", DataType = SqlDbType.NVarChar, DataLength = 128, AllowNull = true },
        new SqlColumn { ColumnName = "UserId", DataType = SqlDbType.NVarChar, DataLength = 450, AllowNull = true },
        new SqlColumn { ColumnName = "BusinessId", DataType = SqlDbType.Int, AllowNull = true },
        new SqlColumn { ColumnName = "SourceContext", DataType = SqlDbType.NVarChar, DataLength = 512, AllowNull = true },
        new SqlColumn { ColumnName = "RequestPath", DataType = SqlDbType.NVarChar, DataLength = 512, AllowNull = true },
        new SqlColumn { ColumnName = "MachineName", DataType = SqlDbType.NVarChar, DataLength = 128, AllowNull = true }
    };

    // Configure TimeStamp column
    columnOptions.TimeStamp.ConvertToUtc = true;

    return columnOptions;
}

// Make Program class accessible for WebApplicationFactory in integration tests
public partial class Program { }