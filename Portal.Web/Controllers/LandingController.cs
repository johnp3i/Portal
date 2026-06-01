using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Web.Models;
using Portal.Web.Services;
using Portal.Web.Services.Email;

namespace Portal.Web.Controllers;

[AllowAnonymous]
public class LandingController : Controller
{
    private readonly IReCaptchaService _reCaptchaService;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<LandingController> _logger;
    private readonly IConfiguration _configuration;

    public LandingController(
        IReCaptchaService reCaptchaService,
        IEmailSender emailSender,
        ILogger<LandingController> logger,
        IConfiguration configuration)
    {
        _reCaptchaService = reCaptchaService;
        _emailSender = emailSender;
        _logger = logger;
        _configuration = configuration;
    }

    [HttpGet]
    [Route("/")]
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        ViewBag.ReCaptchaSiteKey = _configuration["ReCaptcha:SiteKey"] ?? string.Empty;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [AllowAnonymous]
    public async Task<IActionResult> ContactUs([FromForm] ContactUsRequest request)
    {
        // 1. Honeypot check (before reCAPTCHA to save API calls)
        if (!string.IsNullOrWhiteSpace(request.Website))
        {
            _logger.LogWarning("Honeypot triggered for contact form submission from {Email}", request.Email);
            return Ok();
        }

        // 2. reCAPTCHA verification (skip if SecretKey is empty)
        var secretKey = _configuration["ReCaptcha:SecretKey"];
        if (!string.IsNullOrWhiteSpace(secretKey))
        {
            var result = await _reCaptchaService.VerifyAsync(request.RecaptchaToken ?? string.Empty);
            var threshold = _configuration.GetValue<double>("ReCaptcha:ScoreThreshold", 0.5);

            if (!result.Success || result.Score < threshold)
            {
                _logger.LogWarning("reCAPTCHA verification failed. Score: {Score}, Success: {Success}, Action: {Action}",
                    result.Score, result.Success, result.Action);
                return BadRequest("reCAPTCHA verification failed");
            }
        }

        try
        {
            // 3. Build and send notification email
            var inquiryType = string.IsNullOrWhiteSpace(request.InquiryType)
                ? "Contact Request"
                : request.InquiryType;

            var notificationSubject = $"3 Inventors Portal — {inquiryType}";
            var notificationBody = BuildNotificationEmailBody(request);

            await _emailSender.SendEmailAsync(
                "ask@3inventors.com",
                notificationSubject,
                notificationBody,
                EmailDepartmentEnum.Ask);

            // 4. Build and send confirmation email
            var (confirmationSubject, confirmationHtmlBody) = ConfirmationEmailBuilder.Build(
                request.FirstName,
                request.LastName,
                request.Email,
                request.CompanyName,
                request.InquiryType,
                "3 Inventors Portal",
                request.Industry);

            await _emailSender.SendEmailAsync(
                request.Email,
                confirmationSubject,
                confirmationHtmlBody,
                EmailDepartmentEnum.Ask);

            _logger.LogInformation("Contact form submitted successfully. Type: {InquiryType}, Email: {Email}",
                request.InquiryType, request.Email);

            // 5. Return Ok()
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send contact form emails for {Email}", request.Email);
            return BadRequest("Failed to process contact form submission");
        }
    }

    private static string BuildNotificationEmailBody(ContactUsRequest request)
    {
        var html = @"<table border=""1"" cellpadding=""8"" cellspacing=""0"" style=""border-collapse:collapse; font-family:Arial,sans-serif;"">";
        html += BuildNotificationRow("Inquiry Type", request.InquiryType);
        html += BuildNotificationRow("Company Name", request.CompanyName);
        html += BuildNotificationRow("First Name", request.FirstName);
        html += BuildNotificationRow("Last Name", request.LastName);
        html += BuildNotificationRow("Email", request.Email);
        html += BuildNotificationRow("Telephone", request.Telephone);
        html += BuildNotificationRow("Industry", request.Industry);
        html += "</table>";
        return html;
    }

    private static string BuildNotificationRow(string label, string? value)
    {
        var encodedLabel = WebUtility.HtmlEncode(label);
        var encodedValue = WebUtility.HtmlEncode(value ?? string.Empty);
        return $@"<tr><td><strong>{encodedLabel}</strong></td><td>{encodedValue}</td></tr>";
    }
}
