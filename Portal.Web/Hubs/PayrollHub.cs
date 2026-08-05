using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Portal.Web.Hubs;

/// <summary>
/// SignalR hub for payroll real-time notifications.
/// Used for batch email progress broadcasting.
/// </summary>
[Authorize]
public class PayrollHub : Hub
{
    // Empty hub — client subscribes and receives BatchEmailProgress events via IHubContext<PayrollHub>
}
