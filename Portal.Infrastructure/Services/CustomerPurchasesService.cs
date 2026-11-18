using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Repositories.Insight;
using Portal.Infrastructure.Services.CustomerPurchases;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Orchestrates the Customer Purchases Dashboard: tenant check, ONE batched repository read, then the pure
/// calculator. Logs the time of each step so slow loads can be traced. Exceptions bubble to the controller.
/// </summary>
public class CustomerPurchasesService : ICustomerPurchasesService
{
    private const int MaxProductKeyLength = 600;
    private const int MaxFeatureKeyLength = 50;

    private readonly CustomerPurchasesRepository _purchasesRepository;
    private readonly FeatureInterestRepository _featureInterestRepository;
    private readonly CustomerRepository _customerRepository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CustomerPurchasesService> _logger;

    public CustomerPurchasesService(
        CustomerPurchasesRepository purchasesRepository,
        FeatureInterestRepository featureInterestRepository,
        CustomerRepository customerRepository,
        ICurrentTenantService currentTenantService,
        TimeProvider timeProvider,
        ILogger<CustomerPurchasesService> logger)
    {
        _purchasesRepository = purchasesRepository;
        _featureInterestRepository = featureInterestRepository;
        _customerRepository = customerRepository;
        _currentTenantService = currentTenantService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<PurchasesDashboardDto?> GetDashboardAsync(int customerId, PurchasesQuery query)
    {
        var businessId = _currentTenantService.CurrentBusinessId;
        if (businessId <= 0)
            return null;

        var watch = Stopwatch.StartNew();
        var customer = await _customerRepository.GetByIdAndBusinessIdAsync(customerId, businessId);
        var customerMs = watch.ElapsedMilliseconds;
        if (customer == null)
            return null;

        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var (periodStart, periodEnd) = CustomerPurchasesCalculator.ResolvePeriod(query, today);

        watch.Restart();
        var data = await _purchasesRepository.GetDashboardDataAsync(businessId, customerId, periodStart, periodEnd);
        var dataMs = watch.ElapsedMilliseconds;

        watch.Restart();
        PurchasesDashboardDto dto;
        if (data.Invoices.Count == 0)
        {
            dto = CustomerPurchasesCalculator.BuildDashboard(
                customerId, customer.Name, data.Invoices, Array.Empty<ProductLineRow>(), null,
                Array.Empty<CatalogPriceRow>(), null, query, today);
        }
        else
        {
            var currency = CustomerPurchasesCalculator.SelectCurrency(data.Invoices, query, today)!;
            var lines = LinesOfCurrency(data, currency);
            var credit = data.CreditByCurrency.TryGetValue(currency, out var summary) ? summary : new CreditNoteSummaryRow(0, 0m);
            var catalogueCurrency = CustomerPurchasesCalculator.InferCatalogueCurrency(data.PrimaryCurrency);

            dto = CustomerPurchasesCalculator.BuildDashboard(
                customerId, customer.Name, data.Invoices, lines, credit, data.Catalogue, catalogueCurrency, query, today);
        }
        var calcMs = watch.ElapsedMilliseconds;

        _logger.LogInformation(
            "Customer purchases dashboard for customer {CustomerId}: customer lookup {CustomerMs} ms, data read {DataMs} ms ({InvoiceCount} invoices, {LineCount} lines), calculation {CalcMs} ms",
            customerId, customerMs, dataMs, data.Invoices.Count, data.Lines.Count, calcMs);

        return dto;
    }

    public async Task<ProductPurchaseLinesDto?> GetProductPurchaseLinesAsync(int customerId, PurchasesQuery query, string productKey)
    {
        var businessId = _currentTenantService.CurrentBusinessId;
        if (businessId <= 0)
            return null;

        var customer = await _customerRepository.GetByIdAndBusinessIdAsync(customerId, businessId);
        if (customer == null)
            return null;

        if (!IsValidProductKey(productKey))
            return new ProductPurchaseLinesDto();

        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var (periodStart, periodEnd) = CustomerPurchasesCalculator.ResolvePeriod(query, today);

        var data = await _purchasesRepository.GetDashboardDataAsync(businessId, customerId, periodStart, periodEnd);
        if (data.Invoices.Count == 0)
            return new ProductPurchaseLinesDto();

        var currency = CustomerPurchasesCalculator.SelectCurrency(data.Invoices, query, today)!;
        var currencyInvoices = data.Invoices
            .Where(row => string.Equals(row.CurrencyCode, currency, StringComparison.Ordinal))
            .ToList();

        return CustomerPurchasesCalculator.BuildDrillDown(
            LinesOfCurrency(data, currency), currencyInvoices, productKey, periodStart, periodEnd, today);
    }

    public async Task<List<string>> GetRegisteredFeatureKeysAsync(string userId)
    {
        var businessId = _currentTenantService.CurrentBusinessId;
        if (businessId <= 0 || string.IsNullOrWhiteSpace(userId))
            return new List<string>();

        return await _featureInterestRepository.GetFeatureKeysAsync(businessId, userId);
    }

    public async Task<ServiceResult> RegisterFeatureInterestAsync(string userId, string? featureKey)
    {
        if (featureKey == null
            || featureKey.Length < 1
            || featureKey.Length > MaxFeatureKeyLength
            || !PurchasesFeatureKeys.IsValid(featureKey))
        {
            return ServiceResult.Fail("Unknown feature.");
        }

        var businessId = _currentTenantService.CurrentBusinessId;
        if (businessId <= 0 || string.IsNullOrWhiteSpace(userId))
            return ServiceResult.Fail("Unable to register interest.");

        await _featureInterestRepository.InsertIfNotExistsAsync(businessId, userId, featureKey);
        return ServiceResult.Ok();
    }

    /// <summary>Lines of the invoices in one currency (the batched read returns every currency).</summary>
    private static List<ProductLineRow> LinesOfCurrency(PurchasesDataSet data, string currency)
    {
        var invoiceIds = data.Invoices
            .Where(row => string.Equals(row.CurrencyCode, currency, StringComparison.Ordinal))
            .Select(row => row.Id)
            .ToHashSet();
        return data.Lines.Where(line => invoiceIds.Contains(line.InvoiceId)).ToList();
    }

    private static bool IsValidProductKey(string? productKey) =>
        !string.IsNullOrEmpty(productKey)
        && productKey.Length <= MaxProductKeyLength
        && (productKey.StartsWith("C:", StringComparison.Ordinal) || productKey.StartsWith("D:", StringComparison.Ordinal));
}