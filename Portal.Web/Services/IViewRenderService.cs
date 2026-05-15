namespace Portal.Web.Services;

/// <summary>
/// Service for rendering Razor views to HTML strings outside of the normal request pipeline.
/// </summary>
public interface IViewRenderService
{
    Task<string> RenderViewToStringAsync(string viewName, object model);
}
