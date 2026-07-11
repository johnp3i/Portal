namespace Portal.Web.Models.RecurringExpense;

public class RecurringExpenseValidateRequest
{
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
}
