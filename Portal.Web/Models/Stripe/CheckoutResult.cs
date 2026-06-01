namespace Portal.Web.Models.Stripe;

public class CheckoutResult
{
    public bool Success { get; set; }
    public string? CheckoutUrl { get; set; }
    public string? ErrorMessage { get; set; }
    public CheckoutFailureReason? FailureReason { get; set; }
}

public enum CheckoutFailureReason
{
    NoPendingRegistration,
    AlreadyCompleted,
    PlanNotAvailable,
    StripeApiError
}
