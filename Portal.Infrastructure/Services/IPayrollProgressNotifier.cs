namespace Portal.Infrastructure.Services;

/// <summary>
/// Abstraction for broadcasting payroll progress notifications to connected clients.
/// Implemented in the Web layer using SignalR IHubContext.
/// </summary>
public interface IPayrollProgressNotifier
{
    /// <summary>
    /// Sends batch email progress update to a specific user.
    /// </summary>
    /// <param name="userId">The user ID to notify.</param>
    /// <param name="current">Current item number processed.</param>
    /// <param name="total">Total items in the batch.</param>
    /// <param name="lastEmployee">Name of the last employee processed.</param>
    /// <param name="status">Status of the last operation: "sent", "failed", or "skipped".</param>
    Task SendBatchEmailProgressAsync(string userId, int current, int total, string lastEmployee, string status);
}
