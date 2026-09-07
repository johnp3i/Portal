namespace Portal.Infrastructure.Constants;

/// <summary>
/// Stable ids for [notification].[OutboxMessageStatusType] seed rows.
/// Used in queries/code instead of magic numbers (mirrors InvoiceStatusTypeId conventions).
/// </summary>
public static class OutboxMessageStatusTypes
{
    public const int Pending = 1;
    public const int Sent = 2;
    public const int Failed = 3;
}
