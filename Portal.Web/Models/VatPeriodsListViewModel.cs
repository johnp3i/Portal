namespace Portal.Web.Models;

public class VatPeriodsListViewModel
{
    public List<VatPeriodRowViewModel> Periods { get; set; } = new();
    public bool NeedsFirstPeriod { get; set; }
}

public class VatPeriodRowViewModel
{
    public int PeriodId { get; set; }
    public string PeriodLabel { get; set; } = null!;
    public DateOnly PeriodStartDate { get; set; }
    public DateOnly PeriodEndDate { get; set; }
    public string Status { get; set; } = null!; // "Submitted", "Pending", "Not Started"
    public DateTime? SubmittedAtUtc { get; set; }
}
