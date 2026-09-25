using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Portal.Web.Controllers;

/// <summary>
/// Public (unauthenticated) streaming endpoint for business logos.
/// Logos live under the private file-storage root at {FileStorage:BasePath}/{businessId}/logos/{file}
/// and are served here so BOTH authenticated screens and public shared snapshots (invoice/proposal
/// links viewed by customers) can render them with a plain &lt;img src&gt;. Safe to be public because
/// filenames are random GUIDs — the same threat model logos already had as static files, but now
/// redeploy-safe, tenant-isolated, and under a single backup root.
/// </summary>
[AllowAnonymous]
public class LogoImageController : Controller
{
    private readonly IConfiguration _configuration;

    public LogoImageController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".svg"] = "image/svg+xml",
        [".webp"] = "image/webp"
    };

    // GET /logo/{businessId}/{file}
    [HttpGet("/logo/{businessId:int}/{file}")]
    public IActionResult Image(int businessId, string file)
    {
        // Reject anything that isn't a bare filename (defends against path traversal).
        if (string.IsNullOrWhiteSpace(file)
            || file.Contains('/') || file.Contains('\\')
            || file.Contains("..")
            || Path.GetFileName(file) != file)
        {
            return NotFound();
        }

        var basePath = _configuration["FileStorage:BasePath"];
        if (string.IsNullOrWhiteSpace(basePath))
            return NotFound();

        var fullPath = Path.Combine(basePath, businessId.ToString(), "logos", file);

        // Ensure the resolved path stays inside the business's logo folder.
        var expectedRoot = Path.GetFullPath(Path.Combine(basePath, businessId.ToString(), "logos"));
        var resolved = Path.GetFullPath(fullPath);
        if (!resolved.StartsWith(expectedRoot, StringComparison.OrdinalIgnoreCase))
            return NotFound();

        if (!System.IO.File.Exists(resolved))
            return NotFound();

        var ext = Path.GetExtension(resolved);
        var contentType = ContentTypes.TryGetValue(ext, out var ct) ? ct : "application/octet-stream";

        var stream = new FileStream(resolved, FileMode.Open, FileAccess.Read, FileShare.Read);
        return File(stream, contentType);
    }
}
