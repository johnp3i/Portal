using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Portal.Web.Filters;

/// <summary>
/// Result filter that transfers the subscription warning message from HttpContext.Items
/// to ViewData so it can be displayed as a banner in the layout.
/// This is needed because the ModuleAccessAttribute (authorization filter) runs before
/// the controller is instantiated, so it cannot set ViewData directly.
/// </summary>
public class SubscriptionWarningResultFilter : IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.HttpContext.Items.TryGetValue("SubscriptionWarning", out var warning)
            && warning is string warningMessage
            && context.Controller is Controller controller)
        {
            controller.ViewData["SubscriptionWarning"] = warningMessage;
        }
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
        // No action needed after result execution
    }
}
