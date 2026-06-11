using System.Collections.ObjectModel;
using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities.Identity;
using Portal.Infrastructure.Interceptors;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Portal.Web.Extensions;
using Portal.Web.Security;
using Portal.Web.Services;
using Serilog;
using Serilog.Sinks.MSSqlServer;
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
builder.Services.AddScoped<ICsvImportService, CsvImportService>();
builder.Services.AddScoped<VatSubmissionPeriodRepository>(sp =>
    new VatSubmissionPeriodRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<VatSubmissionRepository>(sp =>
    new VatSubmissionRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<IVatPeriodGenerationService, VatPeriodGenerationService>();
builder.Services.AddScoped<IVatSubmissionService, VatSubmissionService>();

// --- reCAPTCHA ---
builder.Services.AddHttpClient<IReCaptchaService, ReCaptchaService>();

// --- Revenue Control ---
builder.Services.AddScoped<PaymentRepository>(sp =>
    new PaymentRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<IFinancialStatusEngine>(sp =>
    new FinancialStatusEngine(
        sp.GetRequiredService<PaymentRepository>(),
        sp.GetRequiredService<InvoiceRepository>(),
        sp.GetRequiredService<CreditNoteRepository>()));
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IReceivablesQueryService, ReceivablesQueryService>();
builder.Services.AddScoped<IVatIntegrationService, VatIntegrationService>();

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

// --- MVC ---
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<Portal.Web.Filters.SetupWizardRedirectFilter>();
    options.Filters.Add<Portal.Web.Filters.SubscriptionWarningResultFilter>();
    options.Filters.Add<Portal.Web.Filters.DemoPermissionFilter>();
});

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