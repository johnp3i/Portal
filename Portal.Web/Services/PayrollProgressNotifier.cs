using Microsoft.AspNetCore.SignalR;
using Portal.Infrastructure.Services;
using Portal.Web.Hubs;

namespace Portal.Web.Services;

/// <summary>
/// SignalR-based implementation of IPayrollProgressNotifier.
/// Broadcasts batch email progress to the requesting user via the PayrollHub.
/// </summary>
public class PayrollProgressNotifier : IPayrollProgressNotifier
{
    private readonly IHubContext<PayrollHub> _hubContext;

    public PayrollProgressNotifier(IHubContext<PayrollHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendBatchEmailProgressAsync(string userId, int current, int total, string lastEmployee, string status)
    {
        await _hubContext.Clients.User(userId).SendAsync("BatchEmailProgress", new
        {
            current,
            total,
            lastEmployee,
            status
        });
    }
}
