# Google reCAPTCHA v3 Integration Guide

**Purpose:** Step-by-step instructions for integrating Google reCAPTCHA v3 into an ASP.NET Core web application with a contact form. This guide is designed to be followed by AI agents or developers to produce a consistent implementation.

**Stack:** ASP.NET Core (Razor views), vanilla JavaScript, server-side verification via Google's siteverify API.

---

## Overview

reCAPTCHA v3 is invisible to users — no checkboxes, no puzzles. It scores each request from 0.0 (likely bot) to 1.0 (likely human) based on behavioral signals. The server verifies the score and rejects submissions below a configurable threshold.

The implementation has three parts:
1. **Configuration** — Store keys in `appsettings.json`
2. **Client-side** — Load the reCAPTCHA script, get a token on form submit
3. **Server-side** — Verify the token against Google's API, check the score

A **honeypot field** is added as a bonus layer to catch simple bots that don't execute JavaScript.

---

## Step 1: Configuration

### 1.1 Add reCAPTCHA keys to `appsettings.json`

Add a `ReCaptcha` section to both `appsettings.json` and `appsettings.Development.json`:

```json
"ReCaptcha": {
    "SiteKey": "<SITE_KEY>",
    "SecretKey": "<SECRET_KEY>",
    "ScoreThreshold": 0.5
}
```

- **SiteKey** — The public key used in the browser (safe to expose in HTML)
- **SecretKey** — The private key used for server-side verification (never expose in client code)
- **ScoreThreshold** — Minimum score to accept (0.5 is a good default; raise to 0.7 for stricter filtering, lower to 0.3 if legitimate users are being blocked)

### 1.2 Create the response model

Create `Models/RecaptchaResponse.cs`:

```csharp
using System.Text.Json.Serialization;

namespace YourApp.Models
{
    public class RecaptchaResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("score")]
        public double Score { get; set; }

        [JsonPropertyName("action")]
        public string? Action { get; set; }

        [JsonPropertyName("challenge_ts")]
        public string? ChallengeTs { get; set; }

        [JsonPropertyName("hostname")]
        public string? Hostname { get; set; }

        [JsonPropertyName("error-codes")]
        public string[]? ErrorCodes { get; set; }
    }
}
```

This model maps to Google's siteverify API response. The `Score` field is the key value — closer to 1.0 means more likely human.

---

## Step 2: Client-Side Integration

### 2.1 Load the reCAPTCHA script in the layout

In the `<head>` section of your layout file (e.g., `_Layout.cshtml`), add:

```razor
@inject Microsoft.Extensions.Configuration.IConfiguration Configuration
```

Then before `</head>`:

```html
<script src="https://www.google.com/recaptcha/api.js?render=@Configuration["ReCaptcha:SiteKey"]"></script>
```

### 2.2 Expose the site key to JavaScript

Before the closing `</body>` tag (or before your form script), add:

```html
<script>
    var RECAPTCHA_SITE_KEY = '@Configuration["ReCaptcha:SiteKey"]';
</script>
```

### 2.3 Get a reCAPTCHA token on form submit

In your form submission JavaScript, get a token before sending the form data:

```javascript
document.getElementById('myForm').addEventListener('submit', async function (e) {
    e.preventDefault();
    
    const btn = document.getElementById('submitBtn');
    btn.disabled = true;
    btn.textContent = 'Sending...';

    const formData = new FormData(this);
    const csrfToken = formData.get('__RequestVerificationToken');

    try {
        // Get reCAPTCHA token
        let recaptchaToken = '';
        try {
            recaptchaToken = await grecaptcha.execute(RECAPTCHA_SITE_KEY, { action: 'contact_form' });
        } catch (recaptchaError) {
            btn.disabled = false;
            btn.textContent = 'Verification failed — try again';
            setTimeout(() => { btn.textContent = 'Send Request'; }, 3000);
            return;
        }
        formData.append('recaptchaToken', recaptchaToken);

        // Submit the form
        const response = await fetch('/YourController/YourAction', {
            method: 'POST',
            headers: { 'RequestVerificationToken': csrfToken },
            body: new URLSearchParams(formData)
        });

        if (response.ok) {
            // Success handling
            this.reset();
        } else {
            btn.textContent = 'Something went wrong — try again';
            setTimeout(() => { btn.textContent = 'Send Request'; }, 3000);
        }
    } catch {
        btn.textContent = 'Connection error — try again';
        setTimeout(() => { btn.textContent = 'Send Request'; }, 3000);
    } finally {
        btn.disabled = false;
        if (btn.textContent === 'Sending...') btn.textContent = 'Send Request';
    }
});
```

**Key points:**
- The `action` parameter (e.g., `'contact_form'`) should describe what the user is doing. Use different actions for different forms (e.g., `'login'`, `'signup'`, `'contact_form'`).
- If `grecaptcha.execute` fails (script blocked, network issue), the form does NOT submit — fail-closed.
- The token is appended to the form data as `recaptchaToken`.

### 2.4 Add a honeypot field to the form

In your HTML form, add a hidden field that real users will never see:

```html
<!-- Honeypot field — hidden from real users, traps bots -->
<div style="position:absolute;left:-9999px;" aria-hidden="true">
    <input type="text" name="website" tabindex="-1" autocomplete="off" />
</div>
```

**Why this works:** Bots fill in every field they find. Real users never see this field (it's positioned off-screen). If the field has a value, the submission is from a bot.

---

## Step 3: Server-Side Verification

### 3.1 Inject IConfiguration into the controller

Add `IConfiguration` to your controller's constructor:

```csharp
private readonly IConfiguration _configuration;

public MyController(IConfiguration configuration, /* other dependencies */)
{
    _configuration = configuration;
    // ...
}
```

### 3.2 Add verification to the form handler

Add `string? recaptchaToken = null` to the action method parameters, then verify before processing:

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> ContactUs(
    string name, string email, /* other fields */,
    string? recaptchaToken = null)
{
    try
    {
        // 1. Honeypot check — bots fill hidden fields, real users don't
        if (!string.IsNullOrEmpty(Request.Form["website"]))
        {
            _logger.LogWarning("Honeypot field filled — likely bot from {Email}", email);
            return Ok(); // Return OK to not tip off the bot, but don't process
        }

        // 2. Verify reCAPTCHA token
        var recaptchaSecretKey = _configuration["ReCaptcha:SecretKey"];
        var scoreThreshold = double.TryParse(
            _configuration["ReCaptcha:ScoreThreshold"], out var t) ? t : 0.5;

        if (!string.IsNullOrEmpty(recaptchaSecretKey))
        {
            using var httpClient = new HttpClient();
            var response = await httpClient.PostAsync(
                "https://www.google.com/recaptcha/api/siteverify",
                new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("secret", recaptchaSecretKey),
                    new KeyValuePair<string, string>("response", recaptchaToken ?? "")
                }));

            var json = await response.Content.ReadAsStringAsync();
            var result = System.Text.Json.JsonSerializer.Deserialize<RecaptchaResponse>(json);

            if (result == null || !result.Success || result.Score < scoreThreshold)
            {
                _logger.LogWarning(
                    "reCAPTCHA failed. Score: {Score}, Success: {Success}, Action: {Action}",
                    result?.Score, result?.Success, result?.Action);
                return BadRequest("reCAPTCHA verification failed");
            }
        }

        // 3. Process the form (send emails, log, etc.)
        // ...

        return Ok();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Form submission failed");
        return BadRequest();
    }
}
```

**Key points:**
- The honeypot check returns `Ok()` (not `BadRequest()`) to avoid tipping off bots that their submission was detected.
- The reCAPTCHA check returns `BadRequest()` with a message — the client-side JS handles this.
- If `SecretKey` is empty/null (not configured), the check is skipped — this allows the form to work in environments where reCAPTCHA isn't set up yet.
- The `Score` is logged for monitoring — you can review logs to tune the threshold.

---

## Verification Flow Summary

```
User clicks Submit
    ↓
JS: grecaptcha.execute(siteKey, { action: 'contact_form' })
    ↓
JS: Appends token to form data
    ↓
JS: POST to server with form data + recaptchaToken
    ↓
Server: Check honeypot field → if filled, return Ok() silently
    ↓
Server: POST token to https://www.google.com/recaptcha/api/siteverify
    ↓
Google returns: { success: true, score: 0.9, action: "contact_form" }
    ↓
Server: score >= 0.5? → Process form
Server: score < 0.5?  → Return BadRequest("reCAPTCHA verification failed")
```

---

## Tuning the Score Threshold

| Threshold | Effect |
|-----------|--------|
| **0.3** | Very permissive — only blocks obvious bots. Use if legitimate users are being rejected. |
| **0.5** | Balanced default — blocks most bots while allowing most humans. Start here. |
| **0.7** | Strict — may block some legitimate users on shared networks or VPNs. Use if spam persists at 0.5. |
| **0.9** | Very strict — will block many legitimate users. Not recommended for public forms. |

Monitor your logs for `reCAPTCHA failed` entries. If you see legitimate emails being blocked (real names, real companies), lower the threshold. If spam still gets through, raise it.

---

## reCAPTCHA v3 Badge

By default, reCAPTCHA v3 shows a small badge in the bottom-right corner of the page. **Always hide it** and add the required attribution text below the form instead. This keeps the UI clean and satisfies Google's terms of service.

### Hide the badge

Add this CSS rule to your main stylesheet (e.g., `landing.css` or `site.css`):

```css
/* ── reCAPTCHA v3 badge (hidden — attribution text shown in form) ── */
.grecaptcha-badge { visibility: hidden; }
```

### Add attribution text below the form

Replace any existing text below the submit button (e.g., "We avoid Spam & Junk Emails") with the required Google attribution:

```html
<p class="mt-4 text-center text-xs text-slate-400">
    This site is protected by reCAPTCHA and the Google
    <a href="https://policies.google.com/privacy" class="underline hover:text-slate-600">Privacy Policy</a> and
    <a href="https://policies.google.com/terms" class="underline hover:text-slate-600">Terms of Service</a> apply.
</p>
```

**This is mandatory when hiding the badge.** Google requires the attribution text to be visible on any page where reCAPTCHA is active. Placing it below the form submit button is the standard approach.

---

## Checklist

Before deploying, verify:

- [ ] `ReCaptcha:SiteKey` and `ReCaptcha:SecretKey` are set in `appsettings.json`
- [ ] The reCAPTCHA script tag is in the layout `<head>` with the correct site key
- [ ] `RECAPTCHA_SITE_KEY` JavaScript variable is set before the form script loads
- [ ] The form submit handler calls `grecaptcha.execute` and appends the token
- [ ] The honeypot field is in the form HTML (hidden off-screen)
- [ ] The controller checks the honeypot field before processing
- [ ] The controller verifies the reCAPTCHA token against Google's API
- [ ] The `RecaptchaResponse` model class exists
- [ ] `IConfiguration` is injected into the controller
- [ ] The score threshold is set (default 0.5)
- [ ] The reCAPTCHA badge is hidden via CSS (`.grecaptcha-badge { visibility: hidden; }`)
- [ ] The Google attribution text is visible below the form submit button
- [ ] Logs capture reCAPTCHA failures for monitoring

---

## Reference Implementation

For a working example, see the MyChair Operational Suite codebase:

- **Config:** `myChair.Web/appsettings.json` → `ReCaptcha` section
- **Model:** `myChair.Web/Models/RecaptchaResponse.cs`
- **Layout:** `myChair.Web/Views/Shared/_Layout.cshtml` → script tag, honeypot field, JS variable
- **Client JS:** `myChair.Web/wwwroot/js/contact-modal.js` → form submit handler with token
- **Controller:** `myChair.Web/Controllers/HomeController.cs` → `ContactUs` action with verification
