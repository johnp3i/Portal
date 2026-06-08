using Microsoft.Extensions.Logging;

namespace Portal.Web.Services.Billing;

/// <summary>
/// Result of a VAT calculation including rate, amounts, and reverse charge status.
/// </summary>
public record VatCalculationResult(
    decimal VatRate,
    decimal VatAmount,
    decimal GrossAmount,
    bool IsReverseCharge,
    string? ReverseChargeNotation);

/// <summary>
/// Defines the contract for VAT calculation based on customer location and VAT registration.
/// </summary>
public interface IVatCalculationService
{
    /// <summary>
    /// Calculates VAT for a subscription invoice based on customer location and VAT registration.
    /// </summary>
    VatCalculationResult Calculate(decimal netAmount, string? customerCountry, string? customerVatNumber);
}

/// <summary>
/// Determines the applicable VAT rate based on customer country and VAT registration status.
/// Applies Cyprus domestic VAT rules (19%), EU reverse-charge mechanism (0% with notation),
/// and handles non-EU customers (0%).
/// </summary>
public class VatCalculationService : IVatCalculationService
{
    private const decimal CyprusVatRate = 0.19m;
    private const string ReverseChargeNotation = "Reverse Charge - Article 196 Council Directive 2006/112/EC";
    private const string CyprusCountryCode = "CY";

    /// <summary>
    /// Current EU member state ISO 3166-1 alpha-2 codes (27 members).
    /// </summary>
    private static readonly string[] EuMemberStates =
    {
        "AT", "BE", "BG", "HR", "CY", "CZ", "DK", "EE", "FI", "FR",
        "DE", "GR", "HU", "IE", "IT", "LV", "LT", "LU", "MT", "NL",
        "PL", "PT", "RO", "SK", "SI", "ES", "SE"
    };

    private readonly ILogger<VatCalculationService> _logger;

    public VatCalculationService(ILogger<VatCalculationService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public VatCalculationResult Calculate(decimal netAmount, string? customerCountry, string? customerVatNumber)
    {
        var vatRate = DetermineVatRate(customerCountry, customerVatNumber, out var isReverseCharge);

        var vatAmount = netAmount * vatRate;
        var grossAmount = netAmount + vatAmount;
        var notation = isReverseCharge ? ReverseChargeNotation : null;

        return new VatCalculationResult(vatRate, vatAmount, grossAmount, isReverseCharge, notation);
    }

    private decimal DetermineVatRate(string? customerCountry, string? customerVatNumber, out bool isReverseCharge)
    {
        isReverseCharge = false;

        // Null or empty country → default to 19% and log warning
        if (string.IsNullOrWhiteSpace(customerCountry))
        {
            _logger.LogWarning(
                "Customer country is null or empty — defaulting to Cyprus VAT rate of {VatRate}%",
                CyprusVatRate * 100);
            return CyprusVatRate;
        }

        var country = customerCountry.Trim().ToUpperInvariant();

        // Cyprus (domestic) → 19%
        if (country == CyprusCountryCode)
        {
            return CyprusVatRate;
        }

        // EU member state (not Cyprus)
        if (IsEuMemberState(country))
        {
            // Has VAT number → 0% reverse charge
            if (!string.IsNullOrWhiteSpace(customerVatNumber))
            {
                isReverseCharge = true;
                return 0m;
            }

            // No VAT number → 19%
            return CyprusVatRate;
        }

        // Non-EU → 0% (no reverse charge)
        return 0m;
    }

    private static bool IsEuMemberState(string countryCode)
    {
        return EuMemberStates.Contains(countryCode, StringComparer.OrdinalIgnoreCase);
    }
}
