namespace Portal.Web.Models.Stripe;

public class SetupWizardResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, string> ValidationErrors { get; set; } = new();
}
