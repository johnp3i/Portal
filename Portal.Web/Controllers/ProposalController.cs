using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Services;

namespace Portal.Web.Controllers;

/// <summary>
/// Public, unauthenticated controller for viewing shared proposals via token-based URLs.
/// </summary>
[AllowAnonymous]
public class ProposalController : Controller
{
    private readonly IProposalService _proposalService;
    private readonly IProposalAcceptanceService _acceptanceService;

    public ProposalController(IProposalService proposalService, IProposalAcceptanceService acceptanceService)
    {
        _proposalService = proposalService;
        _acceptanceService = acceptanceService;
    }

    [HttpGet("/proposal/{token}")]
    public async Task<IActionResult> ViewProposal(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return NotFound();

        var share = await _proposalService.GetByTokenAsync(token);
        if (share == null)
            return NotFound();

        // Set cache-control to prevent intermediary caching
        Response.Headers["Cache-Control"] = "no-store";

        // Check if cancelled or expired
        if (!share.IsActive || share.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return View("~/Views/Shared/Unavailable.cshtml");
        }

        // Inject acceptance UI into the HTML snapshot
        var html = share.SnapshotHtml;
        var pageWrap = html.IndexOf("<div class=\"page-wrap\">");
        if (pageWrap >= 0)
        {
            // Check if an acceptance already exists for this share
            var acceptance = await _acceptanceService.GetByProposalShareIdAsync(share.Id);
            string acceptanceHtml;

            if (acceptance != null)
            {
                // Read-only accepted message
                acceptanceHtml = $@"<div class=""no-print"" style=""text-align:center;margin-top:20px;margin-bottom:20px;"">
                    <div style=""display:inline-flex;align-items:center;gap:8px;padding:12px 24px;background:#e6f7f1;border:1px solid #129867;border-radius:12px;color:#129867;font-size:14px;font-weight:700;font-family:inherit;"">
                        &#x2713; Accepted on <span>{acceptance.AcceptedAtUtc:dd MMM yyyy}</span>
                    </div>
                </div>";
            }
            else
            {
                // Acceptance form with checkbox and button
                acceptanceHtml = $@"<div class=""no-print"" id=""acceptance-section"" style=""text-align:center;margin-top:20px;margin-bottom:20px;"">
                    <div style=""display:inline-block;padding:20px 32px;background:#f8fbff;border:1px solid #d8e4ef;border-radius:14px;"">
                        <label style=""display:flex;align-items:center;gap:10px;cursor:pointer;font-size:14px;color:#2d3748;font-family:inherit;margin-bottom:14px;"">
                            <input type=""checkbox"" id=""acceptTermsCheckbox"" style=""width:18px;height:18px;cursor:pointer;"" />
                            I accept this proposal and agree to proceed with the quoted work.
                        </label>
                        <button id=""acceptProposalBtn"" disabled style=""padding:10px 24px;background:linear-gradient(180deg,#1A6BB8 0%,#0D5EA6 100%);color:#fff;border:none;border-radius:12px;font-size:13px;font-weight:700;cursor:pointer;font-family:inherit;opacity:0.5;"">
                            Accept Proposal
                        </button>
                    </div>
                    <script>
                        (function() {{
                            var checkbox = document.getElementById('acceptTermsCheckbox');
                            var btn = document.getElementById('acceptProposalBtn');
                            checkbox.addEventListener('change', function() {{
                                btn.disabled = !checkbox.checked;
                                btn.style.opacity = checkbox.checked ? '1' : '0.5';
                            }});
                            btn.addEventListener('click', async function() {{
                                if (!checkbox.checked) return;
                                BlockUI.show('Processing...');
                                try {{
                                    var response = await fetch('/proposal/{token}/accept', {{
                                        method: 'POST',
                                        headers: {{ 'Content-Type': 'application/json' }}
                                    }});
                                    var data = await response.json();
                                    BlockUI.hide();
                                    if (data.success || data.alreadyAccepted) {{
                                        var acceptedDate = new Date(data.acceptedAt).toLocaleDateString();
                                        Swal.fire({{ title: 'Proposal Accepted', text: 'Thank you for accepting this proposal.', icon: 'success', confirmButtonColor: '#0D5EA6' }});
                                        document.getElementById('acceptance-section').innerHTML = '<div style=""display:inline-flex;align-items:center;gap:8px;padding:12px 24px;background:#e6f7f1;border:1px solid #129867;border-radius:12px;color:#129867;font-size:14px;font-weight:700;font-family:inherit;"">&#x2713; Accepted on ' + acceptedDate + '</div>';
                                    }} else {{
                                        Swal.fire({{ title: 'Error', text: data.message || 'An unexpected error occurred. Please try again.', icon: 'error', confirmButtonColor: '#0D5EA6' }});
                                    }}
                                }} catch (e) {{
                                    BlockUI.hide();
                                    Swal.fire({{ title: 'Error', text: 'An unexpected error occurred. Please try again.', icon: 'error', confirmButtonColor: '#0D5EA6' }});
                                }}
                            }});
                        }})();
                    </script>
                </div>";
            }

            html = html.Insert(pageWrap, acceptanceHtml);
        }

        return Content(html, "text/html");
    }

    [HttpPost("/proposal/{token}/accept")]
    public async Task<IActionResult> AcceptProposal(string token)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = Request.Headers["User-Agent"].ToString();

        var result = await _acceptanceService.AcceptAsync(token, ipAddress, userAgent);

        return Json(new
        {
            success = result.Success,
            message = result.Message,
            acceptedAt = result.AcceptedAtUtc,
            alreadyAccepted = result.AlreadyAccepted
        });
    }
}
