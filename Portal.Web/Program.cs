using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities.Identity;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Portal.Web.Extensions;
using Portal.Web.Security;
using Portal.Web.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// --- Database Contexts ---
builder.Services.AddDbContext<PortalDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("PortalDb")));

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

// --- Application Services ---
builder.Services.AddHttpContextAccessor();
builder.Services.ConfigureWebsiteSettings(builder.Configuration);
builder.Services.ConfigureEmailAccounts(builder.Configuration);
builder.Services.ConfigureEmail();
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
builder.Services.AddScoped<ICurrentTenantService, CurrentTenantService>();
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

// --- MVC ---
builder.Services.AddControllersWithViews();

// --- Serilog ---
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Portal.Web")
    .Enrich.WithCorrelationId()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File("logs/portal-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{CorrelationId}] [{UserId}] [{BusinessId}] {Message:lj}{NewLine}{Exception}"));

var app = builder.Build();

// --- Seed Data (Development) ---
using (var scope = app.Services.CreateScope())
{
    await Portal.Web.Data.SeedData.InitializeAsync(scope.ServiceProvider);
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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
