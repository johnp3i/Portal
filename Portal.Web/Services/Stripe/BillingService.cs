using Microsoft.Extensions.Options;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Portal.Web.Configuration;
using Portal.Web.Models.Stripe;
using Portal.Web.Services.Billing;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace Portal.Web.Services.Stripe;

/// <summary>
/// Provides billing history, invoice retrieval, and PDF invoice generation
/// for business owners viewing their payment records.
/// </summary>
public class BillingService : IBillingService
{
    private readonly SubscriptionRepository _subscriptionRepository;
    private readonly BillingInvoiceRepository _billingInvoiceRepository;
    private readonly BillingPaymentRepository _billingPaymentRepository;
    private readonly IPlanRepository _planRepository;
    private readonly IBusinessService _businessService;
    private readonly IViewRenderService _viewRenderService;
    private readonly IVatCalculationService _vatCalculationService;
    private readonly InvoiceSettings _invoiceSettings;
    private readonly ILogger<BillingService> _logger;

    public BillingService(
        SubscriptionRepository subscriptionRepository,
        BillingInvoiceRepository billingInvoiceRepository,
        BillingPaymentRepository billingPaymentRepository,
        IPlanRepository planRepository,
        IBusinessService businessService,
        IViewRenderService viewRenderService,
        IVatCalculationService vatCalculationService,
        IOptions<InvoiceSettings> invoiceSettings,
        ILogger<BillingService> logger)
    {
        _subscriptionRepository = subscriptionRepository;
        _billingInvoiceRepository = billingInvoiceRepository;
        _billingPaymentRepository = billingPaymentRepository;
        _planRepository = planRepository;
        _businessService = businessService;
        _viewRenderService = viewRenderService;
        _vatCalculationService = vatCalculationService;
        _invoiceSettings = invoiceSettings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<BillingOverviewModel> GetBillingOverviewAsync(int businessId)
    {
        try
        {
            var subscription = await _subscriptionRepository.GetByBusinessIdAsync(businessId);

            if (subscription == null)
            {
                _logger.LogWarning(
                    "No subscription found for business {BusinessId} when loading billing overview",
                    businessId);

                return new BillingOverviewModel
                {
                    PlanName = string.Empty,
                    SubscriptionStatus = string.Empty,
                    CurrentPeriodStart = DateTime.MinValue,
                    CurrentPeriodEnd = DateTime.MinValue,
                    NextRenewalDate = null
                };
            }

            var plan = await _planRepository.GetByIdAsync(subscription.PlanId);

            var nextRenewalDate = subscription.Status is "active" or "trialing"
                ? subscription.CurrentPeriodEnd
                : (DateTime?)null;

            return new BillingOverviewModel
            {
                PlanName = plan?.Name ?? "Unknown Plan",
                SubscriptionStatus = subscription.Status,
                CurrentPeriodStart = subscription.CurrentPeriodStart,
                CurrentPeriodEnd = subscription.CurrentPeriodEnd,
                NextRenewalDate = nextRenewalDate
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error loading billing overview for business {BusinessId}",
                businessId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<PagedResult<BillingInvoiceModel>> GetInvoicesAsync(int businessId, int page, int pageSize)
    {
        try
        {
            var (items, totalCount) = await _billingInvoiceRepository.GetByBusinessIdPagedAsync(businessId, page, pageSize);

            var invoiceModels = items.Select(invoice => new BillingInvoiceModel
            {
                Id = invoice.Id,
                InvoiceDate = invoice.CreatedAtUtc,
                PeriodStart = invoice.PeriodStart,
                PeriodEnd = invoice.PeriodEnd,
                AmountEur = invoice.AmountEur,
                Status = invoice.Status,
                PaidAtUtc = invoice.PaidAtUtc
            }).ToList();

            return new PagedResult<BillingInvoiceModel>
            {
                Items = invoiceModels,
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error loading invoices for business {BusinessId}, page {Page}, pageSize {PageSize}",
                businessId, page, pageSize);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<byte[]> GenerateInvoicePdfAsync(int invoiceId, int businessId)
    {
        try
        {
            var invoice = await _billingInvoiceRepository.GetByIdAsync(invoiceId, businessId);

            if (invoice == null)
            {
                _logger.LogWarning(
                    "Invoice {InvoiceId} not found for business {BusinessId}",
                    invoiceId, businessId);
                throw new InvalidOperationException($"Invoice {invoiceId} not found for business {businessId}.");
            }

            // Get payment info for the invoice — load ALL payments (not just first) for instalment support
            var payments = await _billingPaymentRepository.GetByInvoiceIdAsync(invoiceId);
            var amountPaid = payments.Sum(p => p.AmountEur);
            var outstanding = invoice.AmountEur - amountPaid;
            var isPartiallyPaid = invoice.Status == "partially_paid";
            var firstPayment = payments.FirstOrDefault();

            // Get subscription to resolve plan name
            var subscription = await _subscriptionRepository.GetByBusinessIdAsync(businessId);
            var plan = subscription != null
                ? await _planRepository.GetByIdAsync(subscription.PlanId)
                : null;

            // Get business details
            var business = await _businessService.GetBusinessByIdAsync(businessId);
            var profile = await _businessService.GetBusinessProfileAsync(businessId);

            var planName = plan?.Name ?? "Subscription";
            var unitPrice = invoice.AmountEur;

            // Use persisted InvoiceNumber; fall back to legacy format for NULL records
            var invoiceNumber = invoice.InvoiceNumber ?? $"INV-{invoice.Id:D6}";

            // Calculate VAT based on customer country and VAT registration
            var vatResult = _vatCalculationService.Calculate(
                unitPrice,
                profile?.Country,
                profile?.VatRegistrationNumber);

            // Build the PDF model
            var pdfModel = new BillingInvoicePdfModel
            {
                // Issuer details from InvoiceSettings
                CompanyName = _invoiceSettings.CompanyName,
                CompanyAddress = _invoiceSettings.CompanyAddress,
                CompanyCountryCode = _invoiceSettings.CompanyCountryCode,
                CompanyVatNumber = _invoiceSettings.CompanyVatNumber,
                CompanyEmail = _invoiceSettings.CompanyEmail,

                // Subscribing business details
                BusinessName = business?.Name ?? string.Empty,
                VatNumber = profile?.VatRegistrationNumber,
                SubscriberVatNumber = profile?.VatRegistrationNumber,
                AddressLine1 = profile?.AddressLine1,
                AddressLine2 = profile?.AddressLine2,
                City = profile?.City,
                PostalCode = profile?.PostalCode,
                Country = profile?.Country,

                // Invoice details
                InvoiceNumber = invoiceNumber,
                InvoiceDate = invoice.CreatedAtUtc,
                PeriodStart = invoice.PeriodStart,
                PeriodEnd = invoice.PeriodEnd,

                // Line items
                LineItems = new List<BillingInvoiceLineItem>
                {
                    new BillingInvoiceLineItem
                    {
                        Description = $"{planName} Plan",
                        Quantity = 1,
                        UnitPrice = unitPrice,
                        Total = unitPrice
                    }
                },

                // Totals
                Subtotal = unitPrice,
                VatRate = vatResult.VatRate,
                VatAmount = vatResult.VatAmount,
                Total = unitPrice + vatResult.VatAmount,
                IsReverseCharge = vatResult.IsReverseCharge,
                ReverseChargeNotation = vatResult.ReverseChargeNotation,

                // Payment info
                PaymentMethod = payments.Count == 1 ? firstPayment?.Method : (payments.Count > 1 ? "Multiple" : null),
                PaymentDate = firstPayment?.PaidAtUtc,

                // Multi-payment / instalment support
                Payments = payments.Select(p => new PaymentLineItem
                {
                    Amount = p.AmountEur,
                    Method = p.Method,
                    PaidAtUtc = p.PaidAtUtc,
                    Reference = p.Reference
                }).ToList(),
                AmountPaid = amountPaid,
                Outstanding = outstanding > 0 ? outstanding : 0,
                IsPartiallyPaid = isPartiallyPaid
            };

            // Render the Razor view to HTML
            var html = await _viewRenderService.RenderViewToStringAsync(
                "~/Views/Billing/_InvoicePdf.cshtml", pdfModel);

            // Generate PDF from HTML using Puppeteer (same pattern as StatementRenderer)
            byte[] pdfBytes;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            pdfBytes = await GeneratePdfFromHtmlAsync(html, cts.Token);

            _logger.LogInformation(
                "Generated PDF for invoice {InvoiceId}, business {BusinessId}",
                invoiceId, businessId);

            return pdfBytes;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogError(
                "PDF generation timed out for invoice {InvoiceId}, business {BusinessId}",
                invoiceId, businessId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error generating PDF for invoice {InvoiceId}, business {BusinessId}",
                invoiceId, businessId);
            throw;
        }
    }

    private static async Task<byte[]> GeneratePdfFromHtmlAsync(string html, CancellationToken cancellationToken)
    {
        await new BrowserFetcher().DownloadAsync();

        await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
        });

        await using var page = await browser.NewPageAsync();

        await page.SetContentAsync(html, new NavigationOptions
        {
            WaitUntil = new[] { WaitUntilNavigation.Networkidle0 }
        });

        var pdfBytes = await page.PdfDataAsync(new PdfOptions
        {
            Format = PaperFormat.A4,
            PrintBackground = true,
            MarginOptions = new MarginOptions
            {
                Top = "15mm",
                Bottom = "15mm",
                Left = "15mm",
                Right = "15mm"
            }
        });

        cancellationToken.ThrowIfCancellationRequested();

        return pdfBytes;
    }
}
