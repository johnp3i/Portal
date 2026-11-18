using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services.CustomerPurchases;

public static partial class CustomerPurchasesCalculator
{
    public const string LineStatusWrittenOff = "Written off";
    public const string LineStatusPaid = "Paid";
    public const string LineStatusOverdue = "Overdue";
    public const string LineStatusPartiallyPaid = "Partially paid";
    public const string LineStatusUnpaid = "Unpaid";

    /// <summary>
    /// Pending invoices (not written off, outstanding greater than zero) sorted by due date then
    /// ordinal invoice number, with their outstanding total. Same set as the Pending KPI.
    /// </summary>
    public static (List<PendingInvoiceDto> Items, decimal PendingTotal) BuildPendingList(
        IReadOnlyList<InvoiceFinancialRow> rows, DateOnly today)
    {
        var pending = rows
            .Where(IsPending)
            .OrderBy(row => row.DueDate)
            .ThenBy(row => row.InvoiceNumber, StringComparer.Ordinal)
            .ToList();

        var items = new List<PendingInvoiceDto>(pending.Count);
        foreach (var row in pending)
        {
            var (kind, days, text) = BuildPendingStatus(row, today);
            items.Add(new PendingInvoiceDto
            {
                InvoiceId = row.Id,
                InvoiceNumber = row.InvoiceNumber,
                IssueDate = row.InvoiceDate,
                DueDate = row.DueDate,
                Total = row.TotalAmount,
                Paid = row.TotalPaid,
                CreditNotes = row.TotalCredited,
                Outstanding = Outstanding(row),
                StatusKind = kind,
                Days = days,
                StatusText = text
            });
        }

        return (items, Round2(items.Sum(item => item.Outstanding)));
    }

    /// <summary>Status kind, days and pill text for a pending invoice.</summary>
    public static (string Kind, int Days, string Text) BuildPendingStatus(InvoiceFinancialRow row, DateOnly today)
    {
        if (row.DueDate < today)
        {
            var late = Math.Max(1, today.DayNumber - row.DueDate.DayNumber);
            return ("overdue", late, BuildPillText("overdue", late));
        }

        var kind = row.TotalPaid > 0m || row.TotalCredited > 0m ? "partial" : "unpaid";
        var until = row.DueDate.DayNumber - today.DayNumber;
        return (kind, until, BuildPillText(kind, until));
    }

    /// <summary>
    /// Pill text. kind is "overdue", "partial" or "unpaid"; days is overdue days or days until due.
    /// </summary>
    public static string BuildPillText(string kind, int days)
    {
        if (kind == "overdue")
        {
            var n = Math.Max(1, days);
            return n == 1 ? "Overdue 1 day" : $"Overdue {n} days";
        }

        var prefix = kind == "partial" ? "Partially paid" : "Unpaid";
        if (days <= 0)
        {
            return $"{prefix}, due today";
        }

        return days == 1 ? $"{prefix}, due in 1 day" : $"{prefix}, due in {days} days";
    }

    /// <summary>Invoice line status; Written off is checked first.</summary>
    public static string LineStatus(InvoiceFinancialRow invoice, DateOnly today)
    {
        if (invoice.IsWrittenOff)
        {
            return LineStatusWrittenOff;
        }

        if (Outstanding(invoice) <= 0m)
        {
            return LineStatusPaid;
        }

        if (invoice.DueDate < today)
        {
            return LineStatusOverdue;
        }

        if (invoice.TotalPaid > 0m || invoice.TotalCredited > 0m)
        {
            return LineStatusPartiallyPaid;
        }

        return LineStatusUnpaid;
    }
}
