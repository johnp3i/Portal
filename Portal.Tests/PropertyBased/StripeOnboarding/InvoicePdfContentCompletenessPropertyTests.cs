using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Entities.Billing;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Portal.Web.Models.Stripe;
using Portal.Web.Services;
using Portal.Web.Services.Stripe;
using Xunit;

namespace Portal.Tests.PropertyBased.StripeOnboarding;

// Feature: stripe-onboarding, Property 14: Invoice PDF content completeness

/// <summary>
/// Property-based tests for invoice PDF content completeness.
/// For any billing invoice with an associated Business and BusinessProfile, the generated PDF model
/// SHALL contain: the 3 Inventors company header, the business name, VAT number (if available),
/// address (if available), invoice number (derived from Id), invoice date, period covered,
/// plan name as line item with quantity 1 and unit price, subtotal, total amount, payment method,
/// and payment date.
/// **Validates: Requirements 9.4, 9.5, 9.6**
/// </summary>
public class InvoicePdfContentCompletenessPropertyTests
{
    /// <summary>
    /// Builds a BillingInvoicePdfModel using the same logic as BillingService.GenerateInvoicePdfAsync,
    /// given the input data. This isolates the model-building logic for property testing without
    /// requiring PDF rendering or database access.
    /// </summary>
    private static BillingInvoicePdfModel BuildPdfModel(
        BillingInvoice invoice,
        BillingPayment? payment,
        Plan plan,
        Business business,
        BusinessProfile? profile)
    {
        var planName = plan.Name ?? "Subscription";
        var unitPrice = invoice.AmountEur;

        return new BillingInvoicePdfModel
        {
            // 3 Inventors company header
            CompanyName = "3 Inventors",
            CompanyAddress = "3 Inventors Ltd",

            // Subscribing business details
            BusinessName = business.Name ?? string.Empty,
            VatNumber = profile?.VatRegistrationNumber,
            AddressLine1 = profile?.AddressLine1,
            AddressLine2 = profile?.AddressLine2,
            City = profile?.City,
            PostalCode = profile?.PostalCode,
            Country = profile?.Country,

            // Invoice details
            InvoiceNumber = $"INV-{invoice.Id:D6}",
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
            VatAmount = 0m,
            Total = invoice.AmountEur,

            // Payment info
            PaymentMethod = payment?.Method,
            PaymentDate = payment?.PaidAtUtc
        };
    }

    #region Property 14a: PDF model contains 3 Inventors company header

    /// <summary>
    /// Property 14a: For any valid invoice data, the PDF model SHALL contain the 3 Inventors
    /// company header with company name and address populated.
    /// **Validates: Requirements 9.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PdfModel_ContainsCompanyHeader(
        PositiveInt invoiceIdSeed,
        PositiveInt businessIdSeed,
        NonEmptyString businessNameSeed,
        NonEmptyString planNameSeed,
        PositiveInt amountSeed)
    {
        var invoiceId = (invoiceIdSeed.Get % 999999) + 1;
        var businessId = (businessIdSeed.Get % 1000) + 1;
        var businessName = businessNameSeed.Get.Substring(0, Math.Min(businessNameSeed.Get.Length, 50));
        var planName = planNameSeed.Get.Substring(0, Math.Min(planNameSeed.Get.Length, 30));
        var amount = Math.Round((decimal)(amountSeed.Get % 10000) / 100m + 1m, 2);

        var invoice = CreateInvoice(invoiceId, businessId, amount);
        var payment = CreatePayment(invoiceId, amount);
        var plan = CreatePlan(planName);
        var business = CreateBusiness(businessId, businessName);
        var profile = CreateProfile(businessId);

        var model = BuildPdfModel(invoice, payment, plan, business, profile);

        var hasCompanyName = model.CompanyName == "3 Inventors";
        var hasCompanyAddress = model.CompanyAddress == "3 Inventors Ltd";

        return (hasCompanyName && hasCompanyAddress).ToProperty()
            .Label($"invoiceId={invoiceId}: companyName='{model.CompanyName}', companyAddress='{model.CompanyAddress}'");
    }

    #endregion

    #region Property 14b: PDF model contains business details from profile

    /// <summary>
    /// Property 14b: For any valid invoice with a BusinessProfile, the PDF model SHALL contain
    /// the business name, and VAT number and address when available in the profile.
    /// **Validates: Requirements 9.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PdfModel_ContainsBusinessDetails(
        PositiveInt invoiceIdSeed,
        PositiveInt businessIdSeed,
        NonEmptyString businessNameSeed,
        NonEmptyString planNameSeed,
        NonEmptyString vatNumberSeed,
        NonEmptyString addressSeed,
        NonEmptyString citySeed,
        PositiveInt amountSeed)
    {
        var invoiceId = (invoiceIdSeed.Get % 999999) + 1;
        var businessId = (businessIdSeed.Get % 1000) + 1;
        var businessName = businessNameSeed.Get.Substring(0, Math.Min(businessNameSeed.Get.Length, 50));
        var planName = planNameSeed.Get.Substring(0, Math.Min(planNameSeed.Get.Length, 30));
        var vatNumber = vatNumberSeed.Get.Substring(0, Math.Min(vatNumberSeed.Get.Length, 20));
        var address = addressSeed.Get.Substring(0, Math.Min(addressSeed.Get.Length, 40));
        var city = citySeed.Get.Substring(0, Math.Min(citySeed.Get.Length, 20));
        var amount = Math.Round((decimal)(amountSeed.Get % 10000) / 100m + 1m, 2);

        var invoice = CreateInvoice(invoiceId, businessId, amount);
        var payment = CreatePayment(invoiceId, amount);
        var plan = CreatePlan(planName);
        var business = CreateBusiness(businessId, businessName);
        var profile = new BusinessProfile
        {
            Id = businessId,
            BusinessId = businessId,
            VatRegistrationNumber = vatNumber,
            CompanyRegistrationNumber = "REG-001",
            AddressLine1 = address,
            AddressLine2 = null,
            City = city,
            PostalCode = "12345",
            Country = "Ireland",
            Email = "test@example.com",
            CurrencySymbol = "€",
            VatRegistrationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1)),
            VatPeriodLengthInMonths = 2
        };

        var model = BuildPdfModel(invoice, payment, plan, business, profile);

        var hasBusinessName = model.BusinessName == businessName;
        var hasVatNumber = model.VatNumber == vatNumber;
        var hasAddress = model.AddressLine1 == address;
        var hasCity = model.City == city;

        return (hasBusinessName && hasVatNumber && hasAddress && hasCity).ToProperty()
            .Label($"invoiceId={invoiceId}: businessName='{model.BusinessName}', vat='{model.VatNumber}', " +
                   $"address='{model.AddressLine1}', city='{model.City}'");
    }

    #endregion

    #region Property 14c: PDF model contains correct invoice number, date, and period

    /// <summary>
    /// Property 14c: For any valid invoice, the PDF model SHALL contain the invoice number
    /// formatted as INV-{Id:D6}, the invoice date (CreatedAtUtc), and the period covered.
    /// **Validates: Requirements 9.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PdfModel_ContainsInvoiceNumberDateAndPeriod(
        PositiveInt invoiceIdSeed,
        PositiveInt businessIdSeed,
        NonEmptyString businessNameSeed,
        NonEmptyString planNameSeed,
        PositiveInt amountSeed,
        PositiveInt periodDaysSeed)
    {
        var invoiceId = (invoiceIdSeed.Get % 999999) + 1;
        var businessId = (businessIdSeed.Get % 1000) + 1;
        var businessName = businessNameSeed.Get.Substring(0, Math.Min(businessNameSeed.Get.Length, 50));
        var planName = planNameSeed.Get.Substring(0, Math.Min(planNameSeed.Get.Length, 30));
        var amount = Math.Round((decimal)(amountSeed.Get % 10000) / 100m + 1m, 2);
        var periodDays = (periodDaysSeed.Get % 28) + 1;

        var periodStart = DateTime.UtcNow.AddDays(-periodDays);
        var periodEnd = DateTime.UtcNow;
        var createdAt = periodStart.AddHours(1);

        var invoice = new BillingInvoice
        {
            Id = invoiceId,
            BusinessId = businessId,
            AmountEur = amount,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Status = "paid",
            PaidAtUtc = periodEnd,
            CreatedAtUtc = createdAt
        };

        var payment = CreatePayment(invoiceId, amount);
        var plan = CreatePlan(planName);
        var business = CreateBusiness(businessId, businessName);
        var profile = CreateProfile(businessId);

        var model = BuildPdfModel(invoice, payment, plan, business, profile);

        var expectedInvoiceNumber = $"INV-{invoiceId:D6}";
        var hasCorrectInvoiceNumber = model.InvoiceNumber == expectedInvoiceNumber;
        var hasCorrectDate = model.InvoiceDate == createdAt;
        var hasCorrectPeriodStart = model.PeriodStart == periodStart;
        var hasCorrectPeriodEnd = model.PeriodEnd == periodEnd;

        return (hasCorrectInvoiceNumber && hasCorrectDate && hasCorrectPeriodStart && hasCorrectPeriodEnd).ToProperty()
            .Label($"invoiceId={invoiceId}: number='{model.InvoiceNumber}' (expected '{expectedInvoiceNumber}'), " +
                   $"date={model.InvoiceDate == createdAt}, periodStart={model.PeriodStart == periodStart}, " +
                   $"periodEnd={model.PeriodEnd == periodEnd}");
    }

    #endregion

    #region Property 14d: PDF model contains line item with plan name, quantity 1, and unit price

    /// <summary>
    /// Property 14d: For any valid invoice, the PDF model SHALL contain exactly one line item
    /// with the plan name as description, quantity 1, and unit price equal to the invoice amount.
    /// **Validates: Requirements 9.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PdfModel_ContainsLineItemWithPlanNameQty1AndUnitPrice(
        PositiveInt invoiceIdSeed,
        PositiveInt businessIdSeed,
        NonEmptyString businessNameSeed,
        NonEmptyString planNameSeed,
        PositiveInt amountSeed)
    {
        var invoiceId = (invoiceIdSeed.Get % 999999) + 1;
        var businessId = (businessIdSeed.Get % 1000) + 1;
        var businessName = businessNameSeed.Get.Substring(0, Math.Min(businessNameSeed.Get.Length, 50));
        var planName = planNameSeed.Get.Substring(0, Math.Min(planNameSeed.Get.Length, 30));
        var amount = Math.Round((decimal)(amountSeed.Get % 10000) / 100m + 1m, 2);

        var invoice = CreateInvoice(invoiceId, businessId, amount);
        var payment = CreatePayment(invoiceId, amount);
        var plan = CreatePlan(planName);
        var business = CreateBusiness(businessId, businessName);
        var profile = CreateProfile(businessId);

        var model = BuildPdfModel(invoice, payment, plan, business, profile);

        var hasExactlyOneLineItem = model.LineItems.Count == 1;
        var lineItem = model.LineItems.FirstOrDefault();
        var hasCorrectDescription = lineItem?.Description == $"{planName} Plan";
        var hasQuantityOne = lineItem?.Quantity == 1;
        var hasCorrectUnitPrice = lineItem?.UnitPrice == amount;
        var hasCorrectLineTotal = lineItem?.Total == amount;

        return (hasExactlyOneLineItem && hasCorrectDescription && hasQuantityOne &&
                hasCorrectUnitPrice && hasCorrectLineTotal).ToProperty()
            .Label($"invoiceId={invoiceId}, planName='{planName}', amount={amount}: " +
                   $"lineItems={model.LineItems.Count}, desc='{lineItem?.Description}', " +
                   $"qty={lineItem?.Quantity}, unitPrice={lineItem?.UnitPrice}, total={lineItem?.Total}");
    }

    #endregion

    #region Property 14e: PDF model contains subtotal, VAT amount, and total

    /// <summary>
    /// Property 14e: For any valid invoice, the PDF model SHALL contain subtotal equal to unit price,
    /// VAT amount (0 when not applicable), and total equal to the invoice amount.
    /// **Validates: Requirements 9.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PdfModel_ContainsSubtotalVatAndTotal(
        PositiveInt invoiceIdSeed,
        PositiveInt businessIdSeed,
        NonEmptyString businessNameSeed,
        NonEmptyString planNameSeed,
        PositiveInt amountSeed)
    {
        var invoiceId = (invoiceIdSeed.Get % 999999) + 1;
        var businessId = (businessIdSeed.Get % 1000) + 1;
        var businessName = businessNameSeed.Get.Substring(0, Math.Min(businessNameSeed.Get.Length, 50));
        var planName = planNameSeed.Get.Substring(0, Math.Min(planNameSeed.Get.Length, 30));
        var amount = Math.Round((decimal)(amountSeed.Get % 10000) / 100m + 1m, 2);

        var invoice = CreateInvoice(invoiceId, businessId, amount);
        var payment = CreatePayment(invoiceId, amount);
        var plan = CreatePlan(planName);
        var business = CreateBusiness(businessId, businessName);
        var profile = CreateProfile(businessId);

        var model = BuildPdfModel(invoice, payment, plan, business, profile);

        var hasCorrectSubtotal = model.Subtotal == amount;
        var hasVatAmount = model.VatAmount == 0m; // VAT is 0 when not applicable
        var hasCorrectTotal = model.Total == amount;

        return (hasCorrectSubtotal && hasVatAmount && hasCorrectTotal).ToProperty()
            .Label($"invoiceId={invoiceId}, amount={amount}: subtotal={model.Subtotal}, " +
                   $"vat={model.VatAmount}, total={model.Total}");
    }

    #endregion

    #region Property 14f: PDF model contains payment method and date

    /// <summary>
    /// Property 14f: For any valid invoice with an associated payment, the PDF model SHALL contain
    /// the payment method and payment date from the payment record.
    /// **Validates: Requirements 9.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PdfModel_ContainsPaymentMethodAndDate(
        PositiveInt invoiceIdSeed,
        PositiveInt businessIdSeed,
        NonEmptyString businessNameSeed,
        NonEmptyString planNameSeed,
        NonEmptyString paymentMethodSeed,
        PositiveInt amountSeed,
        PositiveInt daysSeed)
    {
        var invoiceId = (invoiceIdSeed.Get % 999999) + 1;
        var businessId = (businessIdSeed.Get % 1000) + 1;
        var businessName = businessNameSeed.Get.Substring(0, Math.Min(businessNameSeed.Get.Length, 50));
        var planName = planNameSeed.Get.Substring(0, Math.Min(planNameSeed.Get.Length, 30));
        var paymentMethod = paymentMethodSeed.Get.Substring(0, Math.Min(paymentMethodSeed.Get.Length, 20));
        var amount = Math.Round((decimal)(amountSeed.Get % 10000) / 100m + 1m, 2);
        var paidDaysAgo = (daysSeed.Get % 60) + 1;

        var paidAt = DateTime.UtcNow.AddDays(-paidDaysAgo);

        var invoice = CreateInvoice(invoiceId, businessId, amount);
        var payment = new BillingPayment
        {
            Id = 1,
            InvoiceId = invoiceId,
            AmountEur = amount,
            Method = paymentMethod,
            PaidAtUtc = paidAt,
            StripePaymentIntentId = $"pi_{invoiceId}",
            CreatedAtUtc = paidAt
        };
        var plan = CreatePlan(planName);
        var business = CreateBusiness(businessId, businessName);
        var profile = CreateProfile(businessId);

        var model = BuildPdfModel(invoice, payment, plan, business, profile);

        var hasPaymentMethod = model.PaymentMethod == paymentMethod;
        var hasPaymentDate = model.PaymentDate == paidAt;

        return (hasPaymentMethod && hasPaymentDate).ToProperty()
            .Label($"invoiceId={invoiceId}: paymentMethod='{model.PaymentMethod}' (expected '{paymentMethod}'), " +
                   $"paymentDate={model.PaymentDate} (expected {paidAt})");
    }

    #endregion

    #region Property 14g: PDF model handles null profile gracefully (optional fields)

    /// <summary>
    /// Property 14g: For any valid invoice where the BusinessProfile is null, the PDF model SHALL
    /// still contain the business name, invoice details, and line items, with optional fields as null.
    /// **Validates: Requirements 9.4, 9.5, 9.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PdfModel_HandlesNullProfileGracefully(
        PositiveInt invoiceIdSeed,
        PositiveInt businessIdSeed,
        NonEmptyString businessNameSeed,
        NonEmptyString planNameSeed,
        PositiveInt amountSeed)
    {
        var invoiceId = (invoiceIdSeed.Get % 999999) + 1;
        var businessId = (businessIdSeed.Get % 1000) + 1;
        var businessName = businessNameSeed.Get.Substring(0, Math.Min(businessNameSeed.Get.Length, 50));
        var planName = planNameSeed.Get.Substring(0, Math.Min(planNameSeed.Get.Length, 30));
        var amount = Math.Round((decimal)(amountSeed.Get % 10000) / 100m + 1m, 2);

        var invoice = CreateInvoice(invoiceId, businessId, amount);
        var payment = CreatePayment(invoiceId, amount);
        var plan = CreatePlan(planName);
        var business = CreateBusiness(businessId, businessName);

        // Profile is null — optional fields should be null
        var model = BuildPdfModel(invoice, payment, plan, business, null);

        var hasBusinessName = model.BusinessName == businessName;
        var vatIsNull = model.VatNumber == null;
        var addressIsNull = model.AddressLine1 == null;
        var cityIsNull = model.City == null;
        var hasInvoiceNumber = model.InvoiceNumber == $"INV-{invoiceId:D6}";
        var hasLineItem = model.LineItems.Count == 1 && model.LineItems[0].Quantity == 1;
        var hasTotal = model.Total == amount;

        return (hasBusinessName && vatIsNull && addressIsNull && cityIsNull &&
                hasInvoiceNumber && hasLineItem && hasTotal).ToProperty()
            .Label($"invoiceId={invoiceId}: businessName='{model.BusinessName}', vat={model.VatNumber}, " +
                   $"address={model.AddressLine1}, invoiceNumber='{model.InvoiceNumber}', " +
                   $"lineItems={model.LineItems.Count}, total={model.Total}");
    }

    #endregion

    #region Helper Methods

    private static BillingInvoice CreateInvoice(int id, int businessId, decimal amount)
    {
        return new BillingInvoice
        {
            Id = id,
            BusinessId = businessId,
            AmountEur = amount,
            PeriodStart = DateTime.UtcNow.AddDays(-30),
            PeriodEnd = DateTime.UtcNow,
            Status = "paid",
            PaidAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-30)
        };
    }

    private static BillingPayment CreatePayment(int invoiceId, decimal amount)
    {
        return new BillingPayment
        {
            Id = 1,
            InvoiceId = invoiceId,
            AmountEur = amount,
            Method = "card",
            PaidAtUtc = DateTime.UtcNow,
            StripePaymentIntentId = $"pi_{invoiceId}",
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private static Plan CreatePlan(string name)
    {
        return new Plan
        {
            Id = 1,
            Name = name,
            Slug = name.ToLowerInvariant().Replace(" ", "-"),
            MonthlyPriceEur = 29.99m,
            IsActive = true,
            MaxUsers = 5,
            DisplayOrder = 1,
            StripePriceId = "price_test123",
            CreatedAtUtc = DateTime.UtcNow.AddMonths(-6),
            UpdatedAtUtc = DateTime.UtcNow.AddMonths(-6)
        };
    }

    private static Business CreateBusiness(int id, string name)
    {
        return new Business
        {
            Id = id,
            Name = name,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow.AddMonths(-3)
        };
    }

    private static BusinessProfile CreateProfile(int businessId)
    {
        return new BusinessProfile
        {
            Id = businessId,
            BusinessId = businessId,
            VatRegistrationNumber = "IE1234567T",
            CompanyRegistrationNumber = "REG-001",
            AddressLine1 = "123 Main Street",
            AddressLine2 = "Suite 4",
            City = "Dublin",
            PostalCode = "D01 AB12",
            Country = "Ireland",
            Email = "billing@example.com",
            CurrencySymbol = "€",
            VatRegistrationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-2)),
            VatPeriodLengthInMonths = 2
        };
    }

    #endregion
}
