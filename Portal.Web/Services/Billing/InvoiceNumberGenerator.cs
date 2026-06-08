using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Portal.Infrastructure.Repositories;
using Portal.Web.Configuration;

namespace Portal.Web.Services.Billing;

/// <summary>
/// Parsed components of an invoice number string.
/// </summary>
public record InvoiceNumberComponents(string PlatformCode, int Year, int Sequence);

/// <summary>
/// Defines the contract for invoice number generation and format/parse utilities.
/// </summary>
public interface IInvoiceNumberGenerator
{
    /// <summary>
    /// Generates the next sequential invoice number for the current UTC year.
    /// Must be called within an active database transaction.
    /// </summary>
    Task<string> GenerateNextAsync(DateTime utcNow);

    /// <summary>
    /// Formats an invoice number from its components.
    /// Pattern: {PlatformCode}-INV-{yyyy}-{NNNN}
    /// </summary>
    string Format(string platformCode, int year, int sequence);

    /// <summary>
    /// Parses an invoice number string into its components.
    /// Returns null if the format is invalid.
    /// </summary>
    InvoiceNumberComponents? Parse(string invoiceNumber);
}

/// <summary>
/// Generates sequential invoice numbers in the format {PlatformCode}-INV-{yyyy}-{NNNN}.
/// Validates PlatformCode configuration and delegates persistence to IInvoiceSequenceRepository.
/// </summary>
public class InvoiceNumberGenerator : IInvoiceNumberGenerator
{
    private static readonly Regex PlatformCodeRegex = new(@"^[A-Za-z0-9]{1,10}$", RegexOptions.Compiled);
    private static readonly Regex InvoiceNumberRegex = new(@"^([A-Za-z0-9]{1,10})-INV-(\d{4})-(\d{4,})$", RegexOptions.Compiled);

    private readonly IInvoiceSequenceRepository _sequenceRepository;
    private readonly InvoiceSettings _settings;

    public InvoiceNumberGenerator(
        IInvoiceSequenceRepository sequenceRepository,
        IOptions<InvoiceSettings> settings)
    {
        _sequenceRepository = sequenceRepository;
        _settings = settings.Value;
    }

    /// <inheritdoc />
    public async Task<string> GenerateNextAsync(DateTime utcNow)
    {
        ValidatePlatformCode(_settings.PlatformCode);

        var year = utcNow.Year;
        var sequence = await _sequenceRepository.IncrementAndGetAsync(year);

        return Format(_settings.PlatformCode, year, sequence);
    }

    /// <inheritdoc />
    public string Format(string platformCode, int year, int sequence)
    {
        return $"{platformCode}-INV-{year:D4}-{sequence.ToString().PadLeft(4, '0')}";
    }

    /// <inheritdoc />
    public InvoiceNumberComponents? Parse(string invoiceNumber)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            return null;

        var match = InvoiceNumberRegex.Match(invoiceNumber);
        if (!match.Success)
            return null;

        var platformCode = match.Groups[1].Value;
        var year = int.Parse(match.Groups[2].Value);
        var sequence = int.Parse(match.Groups[3].Value);

        return new InvoiceNumberComponents(platformCode, year, sequence);
    }

    /// <summary>
    /// Validates that the PlatformCode is non-null, non-empty, and contains only alphanumeric characters.
    /// Throws InvalidOperationException if validation fails.
    /// </summary>
    private static void ValidatePlatformCode(string? platformCode)
    {
        if (string.IsNullOrEmpty(platformCode) || !PlatformCodeRegex.IsMatch(platformCode))
        {
            throw new InvalidOperationException(
                $"PlatformCode is invalid. It must be 1-10 alphanumeric characters. Current value: '{platformCode ?? "(null)"}'.");
        }
    }
}
