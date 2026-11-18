using System.Globalization;

namespace Portal.Infrastructure.Models;

/// <summary>
/// Validated query for the Customer Purchases dashboard (period and optional currency).
/// </summary>
public sealed record PurchasesQuery(string PeriodKey, DateOnly? From, DateOnly? To, string? Currency)
{
    public const string PeriodLast12Months = "last12m";
    public const string PeriodThisYear = "thisYear";
    public const string PeriodLastYear = "lastYear";
    public const string PeriodAll = "all";
    public const string PeriodCustom = "custom";

    private const string DateFormat = "yyyy-MM-dd";

    private static readonly string[] AllowedPeriodKeys =
    {
        PeriodLast12Months, PeriodThisYear, PeriodLastYear, PeriodAll, PeriodCustom
    };

    public static readonly PurchasesQuery Default = new(PeriodLast12Months, null, null, null);

    /// <summary>
    /// Parses raw request values. Null/empty currency means auto; a non-empty malformed currency
    /// fails the result (callers wanting leniency pass null and use <see cref="NormalizeCurrency"/>).
    /// </summary>
    public static ServiceResult<PurchasesQuery> Parse(string? period, string? from, string? to, string? currency)
    {
        try
        {
            var normalizedCurrency = NormalizeCurrency(currency);
            if (!string.IsNullOrEmpty(currency) && normalizedCurrency is null)
            {
                return ServiceResult<PurchasesQuery>.Fail("Invalid currency.");
            }

            var periodKey = string.IsNullOrEmpty(period) ? PeriodLast12Months : period;

            if (Array.IndexOf(AllowedPeriodKeys, periodKey) < 0)
            {
                return ServiceResult<PurchasesQuery>.Fail("Invalid period.");
            }

            if (periodKey != PeriodCustom)
            {
                return ServiceResult<PurchasesQuery>.Ok(new PurchasesQuery(periodKey, null, null, normalizedCurrency));
            }

            if (!DateOnly.TryParseExact(from, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fromDate))
            {
                return ServiceResult<PurchasesQuery>.Fail("Invalid from date. Use yyyy-MM-dd.");
            }

            if (!DateOnly.TryParseExact(to, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var toDate))
            {
                return ServiceResult<PurchasesQuery>.Fail("Invalid to date. Use yyyy-MM-dd.");
            }

            if (fromDate > toDate)
            {
                return ServiceResult<PurchasesQuery>.Fail("The from date must be on or before the to date.");
            }

            return ServiceResult<PurchasesQuery>.Ok(new PurchasesQuery(PeriodCustom, fromDate, toDate, normalizedCurrency));
        }
        catch (Exception ex)
        {
            return ServiceResult<PurchasesQuery>.Fail("Invalid query.");
        }
    }

    /// <summary>
    /// Returns the trimmed currency when it is exactly three uppercase ASCII letters (^[A-Z]{3}$),
    /// otherwise null (auto). Never throws.
    /// </summary>
    public static string? NormalizeCurrency(string? currency)
    {
        try
        {
            if (currency is null)
            {
                return null;
            }

            var trimmed = currency.Trim();
            if (trimmed.Length != 3)
            {
                return null;
            }

            foreach (var c in trimmed)
            {
                if (c < 'A' || c > 'Z')
                {
                    return null;
                }
            }

            return trimmed;
        }
        catch (Exception ex)
        {
            return null;
        }
    }
}
