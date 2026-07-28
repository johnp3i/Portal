using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Services;
using Portal.Web.Services.Stripe;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace Portal.Web.Controllers;

/// <summary>
/// Public, unauthenticated controller for viewing shared invoices via token-based URLs.
/// </summary>
[AllowAnonymous]
public class InvoiceViewController : Controller
{
    private readonly IInvoiceSharingService _sharingService;
    private readonly IInvoiceAcceptanceService _acceptanceService;
    private readonly IInvoiceRenderer _invoiceRenderer;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogoService _logoService;
    private readonly IPaymentInstructionsService _paymentInstructionsService;
    private readonly PortalDbContext _dbContext;
    private readonly ILogger<InvoiceViewController> _logger;
    private readonly IStripeConnectService _stripeConnectService;

    public InvoiceViewController(
        IInvoiceSharingService sharingService,
        IInvoiceAcceptanceService acceptanceService,
        IInvoiceRenderer invoiceRenderer,
        IWebHostEnvironment environment,
        ILogoService logoService,
        IPaymentInstructionsService paymentInstructionsService,
        PortalDbContext dbContext,
        ILogger<InvoiceViewController> logger,
        IStripeConnectService stripeConnectService)
    {
        _sharingService = sharingService;
        _acceptanceService = acceptanceService;
        _invoiceRenderer = invoiceRenderer;
        _environment = environment;
        _logoService = logoService;
        _paymentInstructionsService = paymentInstructionsService;
        _dbContext = dbContext;
        _logger = logger;
        _stripeConnectService = stripeConnectService;
    }

    [HttpGet("/invoice-view/{token}")]
    public async Task<IActionResult> ViewInvoice(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return NotFound();

        var share = await _sharingService.GetByTokenAsync(token);
        if (share == null)
            return NotFound();

        Response.Headers["Cache-Control"] = "no-store";

        // Check if cancelled or expired
        if (!share.IsActive || share.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return View("~/Views/Shared/Unavailable.cshtml");
        }

        // Inject dependencies (SweetAlert2 + BlockUI) before </head>
        var dependencyScripts = @"<script src=""https://cdn.jsdelivr.net/npm/sweetalert2@11""></script>
    <script src=""/js/block-ui.js""></script>";
        var html = share.SnapshotHtml;
        var headClose = html.IndexOf("</head>");
        if (headClose >= 0)
        {
            html = html.Insert(headClose, dependencyScripts);
        }

        // Inject Download PDF and Print buttons with download script
        var downloadButton = $@"<div class=""no-print"" style=""display:flex;justify-content:flex-end;gap:10px;margin-bottom:16px;"">
            <button onclick=""window.print()"" style=""display:inline-flex;align-items:center;gap:8px;padding:10px 20px;background:#fff;color:#0D5EA6;border:1.5px solid #0D5EA6;border-radius:12px;font-size:13px;font-weight:700;cursor:pointer;font-family:inherit;"">
                &#x1F5B6; Print
            </button>
            <button onclick=""downloadInvoicePdf()"" style=""display:inline-flex;align-items:center;gap:8px;padding:10px 20px;background:linear-gradient(180deg,#1A6BB8 0%, #0D5EA6 100%);color:#fff;border:none;border-radius:12px;font-size:13px;font-weight:700;cursor:pointer;font-family:inherit;"">
                &#x2B73; Download PDF
            </button>
        </div>
        <script>
            async function downloadInvoicePdf() {{
                BlockUI.show('Generating PDF...');
                try {{
                    var response = await fetch('/invoice-view/{token}/download-pdf');
                    if (!response.ok || !response.headers.get('content-type')?.includes('application/pdf')) {{
                        var data = await response.json();
                        BlockUI.hide();
                        Swal.fire({{ title: 'Error', text: data.message || 'Failed to generate PDF.', icon: 'error', confirmButtonColor: '#0D5EA6' }});
                        return;
                    }}
                    var blob = await response.blob();
                    var contentDisposition = response.headers.get('content-disposition');
                    var filename = 'invoice.pdf';
                    if (contentDisposition) {{
                        var match = contentDisposition.match(/filename[^;=\n]*=((['""]).*?\2|[^;\n]*)/);
                        if (match && match[1]) filename = match[1].replace(/['\""]/g, '');
                    }}
                    var url = URL.createObjectURL(blob);
                    var a = document.createElement('a');
                    a.href = url;
                    a.download = filename;
                    document.body.appendChild(a);
                    a.click();
                    document.body.removeChild(a);
                    URL.revokeObjectURL(url);
                    BlockUI.hide();
                }} catch (e) {{
                    BlockUI.hide();
                    Swal.fire({{ title: 'Error', text: 'An unexpected error occurred.', icon: 'error', confirmButtonColor: '#0D5EA6' }});
                }}
            }}
        </script>";

        // Insert after <div class="page"> or at the start of body content
        var pageDiv = html.IndexOf("<div class=\"page\">");
        if (pageDiv >= 0)
        {
            var insertPos = html.IndexOf(">", pageDiv) + 1;
            html = html.Insert(insertPos, downloadButton);

            // Inject acceptance UI after the download button
            var acceptance = await _acceptanceService.GetByInvoiceShareIdAsync(share.Id);
            string acceptanceHtml;

            if (acceptance != null)
            {
                // Read-only accepted message
                acceptanceHtml = $@"<div class=""no-print"" style=""text-align:center;margin-bottom:20px;"">
                    <div style=""display:inline-flex;align-items:center;gap:8px;padding:12px 24px;background:#e6f7f1;border:1px solid #129867;border-radius:12px;color:#129867;font-size:14px;font-weight:700;font-family:inherit;"">
                        &#x2713; Accepted on <span>{acceptance.AcceptedAtUtc:dd MMM yyyy}</span>
                    </div>
                </div>";
            }
            else
            {
                // Acceptance form with checkbox and button
                acceptanceHtml = $@"<div class=""no-print"" id=""acceptance-section"" style=""text-align:center;margin-bottom:20px;"">
                    <div style=""display:inline-block;padding:20px 32px;background:#f8fbff;border:1px solid #d8e4ef;border-radius:14px;"">
                        <label style=""display:flex;align-items:center;gap:10px;cursor:pointer;font-size:14px;color:#2d3748;font-family:inherit;margin-bottom:14px;"">
                            <input type=""checkbox"" id=""acceptance-checkbox"" style=""width:18px;height:18px;cursor:pointer;"" />
                            I accept this invoice as correct and agree to pay by the due date.
                        </label>
                        <button id=""accept-btn"" disabled style=""padding:10px 24px;background:linear-gradient(180deg,#15b37a 0%,#129867 100%);color:#fff;border:none;border-radius:12px;font-size:13px;font-weight:700;cursor:pointer;font-family:inherit;opacity:0.5;"">
                            Accept Invoice
                        </button>
                    </div>
                    <script>
                        (function() {{
                            var checkbox = document.getElementById('acceptance-checkbox');
                            var btn = document.getElementById('accept-btn');
                            checkbox.addEventListener('change', function() {{
                                btn.disabled = !checkbox.checked;
                                btn.style.opacity = checkbox.checked ? '1' : '0.5';
                            }});
                            btn.addEventListener('click', async function() {{
                                if (!checkbox.checked) return;
                                BlockUI.show('Processing...');
                                try {{
                                    var response = await fetch('/invoice-view/{token}/accept', {{
                                        method: 'POST',
                                        headers: {{ 'Content-Type': 'application/json' }}
                                    }});
                                    var data = await response.json();
                                    BlockUI.hide();
                                    if (data.success || data.alreadyAccepted) {{
                                        var acceptedDate = new Date(data.acceptedAt).toLocaleDateString();
                                        Swal.fire({{ title: 'Invoice Accepted', text: 'Thank you for accepting this invoice.', icon: 'success', confirmButtonColor: '#0D5EA6' }});
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

            // Insert acceptance HTML after the download button
            var acceptanceInsertPos = insertPos + downloadButton.Length;
            html = html.Insert(acceptanceInsertPos, acceptanceHtml);

            // Payment Instructions — determine visibility using per-invoice override + business toggle
            var paymentInstructionsHtml = "";
            var invoiceForPI = await _dbContext.Invoices
                .Where(i => i.Id == share.InvoiceId)
                .Select(i => new { i.InvoiceFinancialStatusTypeId, i.PaymentInstructionsOverride })
                .FirstOrDefaultAsync();

            if (invoiceForPI != null)
            {
                var invoiceStatus = invoiceForPI.InvoiceFinancialStatusTypeId;
                var piOverride = invoiceForPI.PaymentInstructionsOverride;

                // Determine if payment instructions should be shown
                bool showPaymentInstructions;
                if (piOverride == 1)
                    showPaymentInstructions = true;  // Force show
                else if (piOverride == 0)
                    showPaymentInstructions = false; // Force hide
                else
                    showPaymentInstructions = await _paymentInstructionsService.IsEnabledForBusinessAsync(share.BusinessId); // Follow business default

                var eligibleStatuses = new[] { 1, 2, 4 }; // Unpaid, PartiallyPaid, Overdue

                if (showPaymentInstructions && eligibleStatuses.Contains(invoiceStatus))
                {
                    // Show "Pay by Bank Transfer" button + hidden modal
                    paymentInstructionsHtml = $@"
            <div class=""no-print"" style=""margin-top:16px;margin-bottom:20px;text-align:center;"">
                <button id=""payByBankTransferBtn"" onclick=""openPaymentInstructionsModal()"" style=""display:inline-flex;align-items:center;justify-content:center;gap:10px;width:100%;max-width:400px;padding:14px 28px;background:#129867;color:#fff;border:none;border-radius:12px;font-size:15px;font-weight:700;cursor:pointer;font-family:inherit;transition:opacity 0.15s;"" onmouseover=""this.style.opacity='0.9'"" onmouseout=""this.style.opacity='1'"">
                    <svg width=""20"" height=""20"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" viewBox=""0 0 24 24""><path d=""M3 21h18""/><path d=""M3 10h18""/><path d=""M5 6l7-3 7 3""/><path d=""M4 10v11""/><path d=""M20 10v11""/><path d=""M8 10v11""/><path d=""M12 10v11""/><path d=""M16 10v11""/></svg>
                    Pay by Bank Transfer
                </button>
            </div>

            <!-- Payment Instructions Modal (hidden) -->
            <div id=""paymentInstructionsModal"" class=""no-print"" style=""display:none;position:fixed;inset:0;z-index:10000;background:rgba(11,27,40,0.4);backdrop-filter:blur(2px);align-items:center;justify-content:center;padding:24px;"">
                <div style=""background:#fff;border-radius:20px;box-shadow:0 12px 48px rgba(11,27,40,0.18);padding:32px;width:100%;max-width:480px;max-height:90vh;overflow-y:auto;"">
                    <div style=""display:flex;align-items:center;justify-content:space-between;margin-bottom:8px;"">
                        <h3 style=""font-family:'Manrope',sans-serif;font-size:18px;font-weight:700;color:#0B1B28;margin:0;"">Bank Transfer Details</h3>
                        <button onclick=""closePaymentInstructionsModal()"" style=""width:32px;height:32px;border-radius:8px;border:none;background:#EEF4F8;cursor:pointer;font-size:16px;color:#5E7385;display:flex;align-items:center;justify-content:center;"">&times;</button>
                    </div>
                    <p style=""font-size:14px;color:#5E7385;margin:0 0 20px;"">Please use the following details to make your payment.</p>
                    <div style=""height:1px;background:#E2EBF3;margin-bottom:20px;""></div>
                    <div id=""paymentInstructionsContent"" style=""text-align:center;color:#5E7385;font-size:14px;padding:20px 0;"">Loading...</div>
                    <div style=""height:1px;background:#E2EBF3;margin:20px 0;""></div>
                    <div style=""background:rgba(200,145,46,0.08);border-radius:10px;padding:12px 16px;font-size:13px;color:#C8912E;line-height:1.5;margin-bottom:20px;"">
                        Please include the reference number in your transfer description to help the business identify your payment.
                    </div>
                    <button id=""declarePaymentBtn"" onclick=""declarePayment()"" style=""display:block;width:100%;padding:14px;background:#129867;color:#fff;border:none;border-radius:10px;font-size:15px;font-weight:700;cursor:pointer;font-family:inherit;transition:opacity 0.15s;"">I've made the payment</button>
                    <p style=""font-size:12px;color:#5E7385;font-style:italic;text-align:center;margin-top:12px;line-height:1.5;"">This will notify the business that you've initiated a bank transfer. The payment will be confirmed once verified.</p>
                </div>
            </div>

            <script>
                function openPaymentInstructionsModal() {{
                    var modal = document.getElementById('paymentInstructionsModal');
                    modal.style.display = 'flex';
                    loadPaymentInstructions();
                }}

                function closePaymentInstructionsModal() {{
                    document.getElementById('paymentInstructionsModal').style.display = 'none';
                }}

                document.getElementById('paymentInstructionsModal').addEventListener('click', function(e) {{
                    if (e.target === this) closePaymentInstructionsModal();
                }});

                async function loadPaymentInstructions() {{
                    var content = document.getElementById('paymentInstructionsContent');
                    content.innerHTML = '<div style=""text-align:center;color:#5E7385;padding:20px;"">Loading...</div>';
                    try {{
                        var response = await fetch('/invoice-view/{token}/payment-instructions');
                        var result = await response.json();
                        if (!result.success) {{
                            content.innerHTML = '<div style=""color:#C24A4A;padding:20px;"">' + piEscapeHtml(result.message) + '</div>';
                            return;
                        }}
                        var d = result.data;
                        var swiftRow = d.swiftBic ? '<div style=""display:flex;justify-content:space-between;padding:10px 0;border-bottom:1px solid #f4f7fa;""><span style=""color:#5E7385;font-size:13px;"">SWIFT/BIC</span><span style=""font-size:14px;font-weight:600;color:#0B1B28;"">' + piEscapeHtml(d.swiftBic) + '</span></div>' : '';
                        content.innerHTML =
                            '<div style=""background:#F0F6FB;border-radius:12px;padding:20px;margin-bottom:20px;text-align:left;"">' +
                            '<div style=""display:flex;justify-content:space-between;padding:8px 0;""><span style=""color:#5E7385;font-size:13px;"">Amount Due</span><span style=""font-size:18px;font-weight:700;color:#0D5EA6;"">' + piEscapeHtml(d.currencySymbol) + d.outstandingAmount.toFixed(2) + '</span></div>' +
                            '<div style=""display:flex;justify-content:space-between;padding:8px 0;""><span style=""color:#5E7385;font-size:13px;"">Due Date</span><span style=""font-size:14px;font-weight:700;color:#0B1B28;"">' + piEscapeHtml(d.dueDate) + '</span></div>' +
                            '<div style=""display:flex;justify-content:space-between;align-items:center;padding:8px 0;""><span style=""color:#5E7385;font-size:13px;"">Reference</span><span style=""font-size:14px;font-weight:700;color:#0B1B28;display:flex;align-items:center;gap:8px;"">' + piEscapeHtml(d.transferReference) + ' <button onclick=""piCopyText(\'' + piEscapeAttr(d.transferReference) + '\')"" style=""border:1px solid #E2EBF3;background:#fff;border-radius:6px;width:28px;height:28px;cursor:pointer;display:flex;align-items:center;justify-content:center;"" title=""Copy Reference"">&#x1F4CB;</button></span></div>' +
                            '</div>' +
                            '<div style=""text-align:left;"">' +
                            '<div style=""display:flex;justify-content:space-between;padding:10px 0;border-bottom:1px solid #f4f7fa;""><span style=""color:#5E7385;font-size:13px;"">Bank Name</span><span style=""font-size:14px;font-weight:600;color:#0B1B28;"">' + piEscapeHtml(d.bankName) + '</span></div>' +
                            '<div style=""display:flex;justify-content:space-between;padding:10px 0;border-bottom:1px solid #f4f7fa;""><span style=""color:#5E7385;font-size:13px;"">Payee Name</span><span style=""font-size:14px;font-weight:600;color:#0B1B28;"">' + piEscapeHtml(d.payeeName) + '</span></div>' +
                            '<div style=""display:flex;justify-content:space-between;align-items:center;padding:10px 0;border-bottom:1px solid #f4f7fa;""><span style=""color:#5E7385;font-size:13px;"">IBAN</span><span style=""font-size:14px;font-weight:600;color:#0B1B28;display:flex;align-items:center;gap:8px;"">' + piEscapeHtml(d.iban) + ' <button onclick=""piCopyText(\'' + piEscapeAttr(d.iban) + '\')"" style=""border:1px solid #E2EBF3;background:#fff;border-radius:6px;width:28px;height:28px;cursor:pointer;display:flex;align-items:center;justify-content:center;"" title=""Copy IBAN"">&#x1F4CB;</button></span></div>' +
                            swiftRow +
                            '</div>';
                    }} catch(e) {{
                        content.innerHTML = '<div style=""color:#C24A4A;padding:20px;"">Failed to load payment instructions.</div>';
                    }}
                }}

                async function declarePayment() {{
                    BlockUI.show('Processing...');
                    try {{
                        var response = await fetch('/invoice-view/{token}/declare-payment', {{ method: 'POST' }});
                        var result = await response.json();
                        BlockUI.hide();
                        if (result.success) {{
                            closePaymentInstructionsModal();
                            Swal.fire({{ title: 'Thank You', text: result.message, icon: 'success', confirmButtonColor: '#0D5EA6' }});
                            var btn = document.getElementById('payByBankTransferBtn');
                            if (btn) btn.parentElement.innerHTML = '<div style=""display:flex;align-items:center;justify-content:center;gap:8px;padding:12px 24px;border-radius:12px;background:rgba(200,145,46,0.1);color:#C8912E;font-size:14px;font-weight:700;"">' +
                                '<svg width=""16"" height=""16"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" viewBox=""0 0 24 24""><circle cx=""12"" cy=""12"" r=""10""/><polyline points=""12,6 12,12 16,14""/></svg>' +
                                'Payment Onboard \u2014 Awaiting Verification</div><p style=""text-align:center;font-size:13px;color:#5E7385;margin-top:12px;"">Thank you. The business has been notified of your payment.</p>';
                        }} else {{
                            Swal.fire({{ title: 'Error', text: result.message, icon: 'error', confirmButtonColor: '#0D5EA6' }});
                        }}
                    }} catch(e) {{
                        BlockUI.hide();
                        Swal.fire({{ title: 'Error', text: 'An unexpected error occurred.', icon: 'error', confirmButtonColor: '#0D5EA6' }});
                    }}
                }}

                function piCopyText(text) {{
                    navigator.clipboard.writeText(text).then(function() {{
                        Swal.fire({{ title: 'Copied!', text: 'Copied to clipboard.', icon: 'success', confirmButtonColor: '#0D5EA6', timer: 1500, showConfirmButton: false }});
                    }});
                }}

                function piEscapeHtml(str) {{
                    if (!str) return '';
                    var d = document.createElement('div');
                    d.appendChild(document.createTextNode(str));
                    return d.innerHTML;
                }}

                function piEscapeAttr(str) {{
                    if (!str) return '';
                    return str.replace(/\\/g, '\\\\').replace(/'/g, ""\\'"").replace(/""/g, '&quot;');
                }}
            </script>";
                }
                else if (invoiceStatus == 6) // PaymentOnboard
                {
                    paymentInstructionsHtml = @"
            <div class=""no-print"" style=""margin-top:16px;margin-bottom:20px;text-align:center;"">
                <div style=""display:inline-flex;align-items:center;gap:8px;padding:12px 24px;border-radius:12px;background:rgba(200,145,46,0.1);color:#C8912E;font-size:14px;font-weight:700;"">
                    <svg width=""16"" height=""16"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" viewBox=""0 0 24 24""><circle cx=""12"" cy=""12"" r=""10""/><polyline points=""12,6 12,12 16,14""/></svg>
                    Payment Onboard &#x2014; Awaiting Verification
                </div>
                <p style=""font-size:13px;color:#5E7385;margin-top:12px;"">Thank you. The business has been notified of your payment.</p>
            </div>";
                }
            }

            // Insert payment instructions after the acceptance section
            if (!string.IsNullOrEmpty(paymentInstructionsHtml))
            {
                var piInsertPos = acceptanceInsertPos + acceptanceHtml.Length;
                html = html.Insert(piInsertPos, paymentInstructionsHtml);
            }

            // Stripe Connect — "Pay by Card" button
            if (invoiceForPI != null)
            {
                var eligibleForCardPayment = new[] { 1, 2, 4 }; // Unpaid, PartiallyPaid, Overdue
                if (eligibleForCardPayment.Contains(invoiceForPI.InvoiceFinancialStatusTypeId))
                {
                    var isStripeConnected = await _stripeConnectService.IsConnectedAsync(share.BusinessId);
                    if (isStripeConnected)
                    {
                        var payByCardHtml = $@"
            <div class=""no-print"" style=""margin-top:12px;margin-bottom:20px;text-align:center;"">
                <form method=""post"" action=""/invoice-view/{token}/pay-by-card"" style=""display:inline;"">
                    <button type=""submit"" style=""display:inline-flex;align-items:center;justify-content:center;gap:10px;width:100%;max-width:400px;padding:14px 28px;background:#0D5EA6;color:#fff;border:none;border-radius:12px;font-size:15px;font-weight:700;cursor:pointer;font-family:inherit;transition:opacity 0.15s;"" onmouseover=""this.style.opacity='0.9'"" onmouseout=""this.style.opacity='1'"">
                        <svg width=""20"" height=""20"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" viewBox=""0 0 24 24""><rect x=""2"" y=""5"" width=""20"" height=""14"" rx=""2""/><path d=""M2 10h20""/></svg>
                        Pay by Card
                    </button>
                </form>
                <p style=""font-size:12px;color:#8a9bac;margin-top:8px;"">Visa, Mastercard, American Express — instant confirmation</p>
            </div>";

                        // Insert after the last payment-related injection
                        var payByCardInsertPos = html.IndexOf("</div>", html.IndexOf("no-print") > 0 ? html.LastIndexOf("Pay by Bank Transfer") : 0);
                        if (payByCardInsertPos < 0)
                        {
                            // Fallback: insert before </body>
                            var bodyClose = html.LastIndexOf("</body>");
                            if (bodyClose >= 0)
                                html = html.Insert(bodyClose, payByCardHtml);
                        }
                        else
                        {
                            // Insert after the bank transfer section's closing div
                            var afterBankTransfer = html.IndexOf("</div>", html.LastIndexOf("Pay by Bank Transfer"));
                            if (afterBankTransfer >= 0)
                            {
                                // Find the end of the containing div (the no-print wrapper)
                                var endOfSection = html.IndexOf("</div>", afterBankTransfer + 6);
                                if (endOfSection >= 0)
                                    html = html.Insert(endOfSection + 6, payByCardHtml);
                                else
                                    html = html.Insert(afterBankTransfer + 6, payByCardHtml);
                            }
                            else
                            {
                                // No bank transfer button — insert after acceptance/download buttons
                                var bodyClose = html.LastIndexOf("</body>");
                                if (bodyClose >= 0)
                                    html = html.Insert(bodyClose, payByCardHtml);
                            }
                        }
                    }
                }
            }
        }

        // Show payment success message if redirected from Stripe
        if (Request.Query.ContainsKey("payment") && Request.Query["payment"] == "success")
        {
            var successBanner = @"<div class=""no-print"" style=""text-align:center;margin-bottom:20px;"">
                <div style=""display:inline-flex;align-items:center;gap:8px;padding:12px 24px;background:#e6f7f1;border:1px solid #129867;border-radius:12px;color:#129867;font-size:14px;font-weight:700;font-family:inherit;"">
                    &#x2713; Payment received — thank you!
                </div>
            </div>";
            var pageDiv2 = html.IndexOf("<div class=\"page\">");
            if (pageDiv2 >= 0)
            {
                var insertAfterPage = html.IndexOf(">", pageDiv2) + 1;
                html = html.Insert(insertAfterPage, successBanner);
            }
        }

        // Show payment error message if checkout failed
        if (TempData["PaymentError"] != null)
        {
            var errorMessage = TempData["PaymentError"]?.ToString() ?? "An error occurred.";
            var errorBanner = $@"<div class=""no-print"" style=""text-align:center;margin-bottom:20px;"">
                <div style=""display:inline-flex;align-items:center;gap:8px;padding:12px 24px;background:#fdeaea;border:1px solid #f5c6c6;border-radius:12px;color:#C24A4A;font-size:14px;font-weight:700;font-family:inherit;"">
                    {errorMessage}
                </div>
                <p style=""font-size:12px;color:#8a9bac;margin-top:8px;"">You can try again or choose bank transfer below.</p>
            </div>";
            var pageDiv3 = html.IndexOf("<div class=\"page\">");
            if (pageDiv3 >= 0)
            {
                var insertAfterPage = html.IndexOf(">", pageDiv3) + 1;
                html = html.Insert(insertAfterPage, errorBanner);
            }
        }

        return Content(html, "text/html");
    }

    [HttpPost("/invoice-view/{token}/accept")]
    public async Task<IActionResult> AcceptInvoice(string token)
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

    [HttpGet("/invoice-view/{token}/download-pdf")]
    public async Task<IActionResult> DownloadPdf(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return NotFound();

        var share = await _sharingService.GetByTokenAsync(token);
        if (share == null)
            return NotFound();

        if (!share.IsActive || share.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            return NotFound();

        try
        {
            // Render the invoice Snapshot view fresh from the database (same as authenticated download)
            var html = await _invoiceRenderer.RenderAsync(share.InvoiceId, share.BusinessId);

            // Post-process HTML to embed logo as base64 data URI
            html = await EmbedLogoAsBase64Async(html, share.BusinessId);

            // Generate PDF with 30-second timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var pdfBytes = await GeneratePdfFromHtmlAsync(html, cts.Token);

            // Extract invoice number for filename
            var invoiceNumber = share.Invoice?.InvoiceNumber ?? "download";
            var filename = GenerateInvoicePdfFilename(invoiceNumber);

            return File(pdfBytes, "application/pdf", filename);
        }
        catch (OperationCanceledException)
        {
            return StatusCode(500, new { success = false, message = "PDF generation timed out. Please try again." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate PDF for shared invoice token {Token}", token);
            return StatusCode(500, new { success = false, message = "Failed to generate PDF. Please try again." });
        }
    }

    [HttpGet("/invoice-view/{token}/payment-instructions")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPaymentInstructions(string token)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token))
                return Json(new { success = false, message = "Invalid token." });

            var share = await _sharingService.GetByTokenAsync(token);
            if (share == null || !share.IsActive || share.ExpiresAtUtc <= DateTimeOffset.UtcNow)
                return Json(new { success = false, message = "This invoice link is no longer active." });

            var data = await _paymentInstructionsService.GetPaymentInstructionsAsync(share.InvoiceId, share.BusinessId);
            if (data == null)
                return Json(new { success = false, message = "Payment instructions are not available for this invoice." });

            return Json(new { success = true, data });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to load payment instructions." });
        }
    }

    [HttpPost("/invoice-view/{token}/declare-payment")]
    [AllowAnonymous]
    public async Task<IActionResult> DeclarePayment(string token)
    {
        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var result = await _paymentInstructionsService.DeclarePaymentAsync(token, ipAddress);
            return Json(new { success = result.Success, message = result.Message, declaredAtUtc = result.DeclaredAtUtc });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred. Please try again." });
        }
    }

    /// <summary>
    /// Creates a Stripe Checkout Session for the invoice and redirects the customer to Stripe.
    /// POST /invoice-view/{token}/pay-by-card
    /// </summary>
    [HttpPost("/invoice-view/{token}/pay-by-card")]
    public async Task<IActionResult> CreateCheckoutSession(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return NotFound();

        // Validate share token and get invoice
        var share = await _sharingService.GetByTokenAsync(token);
        if (share == null || !share.IsActive || share.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            return NotFound();

        var invoice = await _dbContext.Invoices
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == share.InvoiceId);

        if (invoice == null)
            return NotFound();

        // Check business has Stripe connected
        var isConnected = await _stripeConnectService.IsConnectedAsync(invoice.BusinessId);
        if (!isConnected)
        {
            TempData["PaymentError"] = "Card payments are not available for this business.";
            return Redirect($"/invoice-view/{token}");
        }

        // Get customer name for checkout description
        var customer = await _dbContext.Customers
            .IgnoreQueryFilters()
            .Where(c => c.Id == invoice.CustomerId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync();

        // Build success and cancel URLs
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var successUrl = $"{baseUrl}/invoice-view/{token}?payment=success";
        var cancelUrl = $"{baseUrl}/invoice-view/{token}";

        // Create checkout session
        var result = await _stripeConnectService.CreateCheckoutSessionAsync(
            invoice.Id, invoice.BusinessId, successUrl, cancelUrl, customer);

        if (!result.Success)
        {
            TempData["PaymentError"] = result.Message;
            return Redirect($"/invoice-view/{token}");
        }

        // Redirect customer to Stripe Checkout
        return Redirect(result.Data!);
    }

    private async Task<string> EmbedLogoAsBase64Async(string html, int businessId)
    {
        var logos = await _logoService.GetByBusinessIdAsync(businessId);
        var primaryLogo = logos.FirstOrDefault(l => l.IsPrimary) ?? logos.FirstOrDefault();

        var dataUri = GetLogoAsDataUri(primaryLogo);
        if (string.IsNullOrEmpty(dataUri))
            return html;

        var pattern = @"(<img\s[^>]*src\s*=\s*"")(/uploads/[^""]+)("")";
        html = Regex.Replace(html, pattern, $"$1{dataUri}$3", RegexOptions.IgnoreCase);

        return html;
    }

    private string? GetLogoAsDataUri(Infrastructure.Entities.BusinessLogo? logo)
    {
        if (logo == null || string.IsNullOrWhiteSpace(logo.PublicUrl))
            return null;

        try
        {
            var relativePath = logo.PublicUrl.TrimStart('/');
            var filePath = Path.Combine(_environment.WebRootPath, relativePath);

            if (!System.IO.File.Exists(filePath))
                return null;

            var bytes = System.IO.File.ReadAllBytes(filePath);
            var base64 = Convert.ToBase64String(bytes);
            var contentType = logo.ContentType ?? "image/png";

            return $"data:{contentType};base64,{base64}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read logo file for data URI embedding");
            return null;
        }
    }

    private static async Task<byte[]> GeneratePdfFromHtmlAsync(string html, CancellationToken cancellationToken)
    {
        await new BrowserFetcher().DownloadAsync();

        await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
        });

        await using var page = await browser.NewPageAsync();

        await page.SetContentAsync(html, new NavigationOptions
        {
            WaitUntil = new[] { WaitUntilNavigation.Networkidle0 }
        });

        var pdfBytes = await page.PdfDataAsync(new PdfOptions
        {
            Landscape = false,
            Format = PaperFormat.A4,
            PrintBackground = true,
            MarginOptions = new MarginOptions
            {
                Top = "14mm",
                Bottom = "0mm",
                Left = "0mm",
                Right = "0mm"
            }
        });

        cancellationToken.ThrowIfCancellationRequested();

        return pdfBytes;
    }

    private static string? ExtractInvoiceNumberFromHtml(string html)
    {
        // Try to extract invoice number from the HTML snapshot
        var match = Regex.Match(html, @"Invoice\s*#?\s*:?\s*([A-Za-z0-9\-]+)", RegexOptions.IgnoreCase);
        if (match.Success && match.Groups[1].Value.Length > 0)
            return match.Groups[1].Value;

        return null;
    }

    private static string GenerateInvoicePdfFilename(string invoiceNumber)
    {
        var invalidChars = new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };
        var sanitized = new string(invoiceNumber.Where(c => !invalidChars.Contains(c)).ToArray());

        if (string.IsNullOrWhiteSpace(sanitized))
            return "INV-download.pdf";

        return $"INV-{sanitized}.pdf";
    }
}
