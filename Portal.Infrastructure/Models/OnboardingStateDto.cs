namespace Portal.Infrastructure.Models;

public class OnboardingStateDto
{
    public bool IsVisible { get; set; }
    public bool IsCelebration { get; set; }
    public int CompletedCount { get; set; }
    public int TotalSteps => 6;

    public bool HasBusinessProfile { get; set; }
    public bool HasLogo { get; set; }
    public bool HasPaymentDetails { get; set; }
    public bool HasCustomer { get; set; }
    public bool HasQuotationOrInvoice { get; set; }
    public bool HasIssuedInvoice { get; set; }
}
