# 3 Inventors — Confirmation Email Design Guide

**Purpose:** Design specification for branded HTML confirmation emails sent to visitors after contact form submission. This guide is intended as input for Kiro agents building or upgrading reply emails across 3 Inventors product landing pages (WorkforcePi, EOMFA, MyChair, etc.).

**Reference implementation:** `Website.3Inventors/Services/Email/ConfirmationEmailBuilder.cs`
**HTML mocks:** `.kiro/docs/3inventors.com/email-templates/demo-request-confirmation.html` and `general-inquiry-confirmation.html`

---

## Overview

There are two confirmation email templates, selected by inquiry type:

| Inquiry Type | Subject Line | Template |
|---|---|---|
| Demo Request | `3 Inventors — Demo Request Received` | Demo-specific: echoes platform/industry/company, sets demo expectations |
| General Inquiry (default) | `3 Inventors — Message Received` | General: echoes name/email/company, sets response expectations |

Both templates share a common layout wrapper. The builder method signature:

```csharp
public static (string subject, string body) Build(
    string firstName,
    string? lastName = null,
    string? email = null,
    string? companyName = null,
    string? inquiryType = null,
    string? platform = null,
    string? industry = null)
```

---

## Email Layout Structure

All emails follow this vertical structure:

```
┌─────────────────────────────────────┐
│  HEADER                             │
│  Logo + tagline on light background │
├─────────────────────────────────────┤
│  ACCENT LINE (4px solid blue)       │
├─────────────────────────────────────┤
│  BODY                               │
│  Badge → Greeting → Paragraphs      │
├─────────────────────────────────────┤
│  DETAILS CARD (conditional)         │
│  Key-value pairs in grey card       │
├─────────────────────────────────────┤
│  NEXT STEPS                         │
│  Numbered plain text list           │
├─────────────────────────────────────┤
│  DIVIDER (1px solid line)           │
├─────────────────────────────────────┤
│  CLOSING                            │
│  CTA text + signature               │
├─────────────────────────────────────┤
│  FOOTER                             │
│  Company info on dark background    │
└─────────────────────────────────────┘
```

---

## Email Client Compatibility Rules

These rules were established through testing and must be followed:

1. **No CSS gradients** — `linear-gradient` and `radial-gradient` are not supported in Outlook. Use solid `background-color` only.
2. **No `<style>` blocks** — Email clients strip them. All CSS must be inline.
3. **Table-based layout** — Use `<table role="presentation">` for all structural layout. Do not rely on `<div>` for positioning.
4. **Max width 600px** — Standard email width. Use `max-width:600px; width:100%` on the container table.
5. **No fancy number circles** — Styled `<div>` circles for numbered steps don't align consistently across clients. Use plain text: `1. Step text`.
6. **Font stack** — `'Segoe UI', Tahoma, Geneva, Verdana, sans-serif` (web-safe, no Google Fonts in email).
7. **All images must be hosted** — Use absolute URLs to publicly accessible images. Include `alt` text with matching `color` and `font-size` styles for graceful degradation.
8. **Sanitize all user input** — HTML-encode `&`, `<`, `>`, `"` before inserting into the template to prevent XSS.

---

## Colour Palette

| Token | Hex / Value | Usage |
|---|---|---|
| Page background | `#F2F6FA` | Outer wrapper background |
| Card background | `#FFFFFF` | Main email container |
| Header background | `#F7FAFC` | Light header behind logo |
| Header border | `#E2EBF3` | Bottom border of header |
| Accent line | `#0D5EA6` | 4px solid line below header |
| Brand blue | `#0D5EA6` | Badge text, links, accent colour |
| Badge background | `#EBF5FF` | Light blue pill behind badge text |
| Primary text | `#0B1B28` | Headings, strong text |
| Body text | `#3D4F5F` | Paragraphs, closing text |
| Muted text | `#5E7385` | Detail card labels |
| Details card bg | `#F7FAFC` | Grey card background |
| Details card border | `#E2EBF3` | Card border |
| Divider | `#E2EBF3` | Horizontal rule between sections |
| Footer background | `#0B1B28` | Dark footer |
| Footer text | `#FFFFFF` | Company name in footer |
| Footer muted | `#8899A6` | Address, phone in footer |
| Footer link | `#8EDFFF` | Website link in footer |
| Footer tagline | `#5E6D7A` | "Knowledge · Professionalism · Innovation" |

---

## Section-by-Section Specification

### 1. Header

- **Background:** `#F7FAFC` (solid, no gradient)
- **Padding:** `32px 40px`
- **Border:** `border-bottom: 1px solid #E2EBF3`
- **Logo:** Blue version (`logo_blue_web_toolbar.png`), `width="160"`, centred
- **Logo alt text:** `"3 Inventors"` with `color:#0D5EA6; font-size:22px; font-weight:700` (fallback styling when image blocked)
- **Tagline:** "Operational Intelligence Infrastructure", `font-size:12px`, `letter-spacing:0.18em`, `text-transform:uppercase`, `color:#0D5EA6`, `font-weight:600`

**Adapting for other products:** Replace the logo URL and tagline. For example:
- WorkforcePi: `logo_workforcepi.png` + "Workforce Intelligence Platform"
- EOMFA: `logo_eomfa.png` + "Order Reliability Platform"
- MyChair: `logo_mychair.png` + "Operational Suite"

### 2. Accent Line

- **Height:** `4px`
- **Background:** `#0D5EA6` (solid)
- Sits directly below the header with no gap

### 3. Body Section

- **Padding:** `40px 40px 20px 40px`

**Badge:**
- Pill shape: `background-color:#EBF5FF; border-radius:20px; padding: 6px 16px`
- Text: `font-size:12px; font-weight:700; color:#0D5EA6; letter-spacing:0.06em; text-transform:uppercase`
- Content varies: "Demo Request Received" or "Message Received"

**Greeting:**
- `<h1>` tag: `font-size:24px; font-weight:700; color:#0B1B28; line-height:1.3`
- Format: `Hi {{FirstName}},`

**Body paragraphs:**
- `font-size:16px; line-height:1.7; color:#3D4F5F`
- Bold emphasis on "24 hours" using `<strong>`

### 4. Details Card

- **Container:** `background-color:#F7FAFC; border:1px solid #E2EBF3; border-radius:12px; padding:24px`
- **Section padding:** `0 40px 20px 40px`
- **Card title:** `font-size:13px; font-weight:700; color:#0D5EA6; letter-spacing:0.12em; text-transform:uppercase`

**Detail rows (key-value table):**
- Label cell: `font-size:14px; color:#5E7385; width:110px; vertical-align:top; padding:6px 0`
- Value cell: `font-size:14px; color:#0B1B28; font-weight:600; padding:6px 0`

**Demo Request card fields** (conditional — only shown if value provided):
| Field | Source |
|---|---|
| Platform | `platform` parameter |
| Industry | `industry` parameter |
| Company | `companyName` parameter |

**General Inquiry card fields:**
| Field | Source |
|---|---|
| Name | `firstName` + `lastName` |
| Email | `email` parameter |
| Company | `companyName` parameter (conditional) |

### 5. Next Steps

- **Heading:** `<h2>`, `font-size:16px; font-weight:700; color:#0B1B28; margin:0 0 12px 0`
- **Steps:** Plain text paragraphs, `font-size:14px; line-height:1.6; color:#3D4F5F`
- **Format:** `1. Step text` (plain numbered text — no circles, no icons)
- **Row padding:** `8px 0` on each step's `<td>`

**Demo Request steps:**
1. We will contact you to confirm a convenient time
2. A guided walkthrough of the platform tailored to your operations
3. Discussion of how the system fits your specific workflow

**General Inquiry steps:**
1. Your inquiry is reviewed by our team
2. We respond within 24 hours with a personalised reply
3. If relevant, we suggest a call or meeting to discuss further

### 6. Divider

- `height:1px; background-color:#E2EBF3`
- Padding: `0 40px` (inset from edges)

### 7. Closing

- **Padding:** `24px 40px 32px 40px`
- **CTA text:** `font-size:14px; line-height:1.7; color:#3D4F5F`
- **Links:** `color:#0D5EA6; text-decoration:none; font-weight:600`
- **Signature:** "Kind regards," + line break + "**The 3 Inventors Team**" (`color:#0B1B28`)

**Demo Request closing:** "If you have any questions before the demo, reply to this email or contact us at ask@3inventors.com."

**General Inquiry closing:** "In the meantime, feel free to explore our platforms at www.3inventors.com or reply to this email with any additional details."

### 8. Footer

- **Background:** `#0B1B28` (solid dark)
- **Padding:** `28px 40px`
- **Text alignment:** centre
- **Company name:** `font-size:14px; font-weight:700; color:#FFFFFF`
- **Address + phone:** `font-size:12px; color:#8899A6` — Format: `Nicosia, Cyprus · 700 75 700`
- **Website link:** `font-size:12px; color:#8EDFFF; text-decoration:none`
- **Tagline:** `font-size:11px; color:#5E6D7A; letter-spacing:0.08em` — "Knowledge · Professionalism · Innovation"

---

## Adapting for Other Products

When creating confirmation emails for WorkforcePi, EOMFA, or MyChair landing pages:

### What to change

| Element | 3Inventors | Product adaptation |
|---|---|---|
| Logo URL | `logo_blue_web_toolbar.png` | Product-specific logo |
| Tagline | "Operational Intelligence Infrastructure" | Product tagline |
| Badge text | "Demo Request Received" / "Message Received" | Same pattern, product name optional |
| Subject line | "3 Inventors — Demo Request Received" | "WorkforcePi — Demo Request Received" |
| Closing CTA link | `www.3inventors.com` | Product website URL |
| Footer company name | "3 Inventors Limited" | Same (parent company) |
| Accent line colour | `#0D5EA6` | Product brand colour |

### Product brand colours for accent line

| Product | Accent Colour |
|---|---|
| 3 Inventors | `#0D5EA6` (brand blue) |
| WorkforcePi | `#005597` or `#00BCF2` (workforce blue) |
| EOMFA | `#129A5B` or `#16A34A` (green) |
| MyChair | `#0D5EA6` (blue, same as parent) |

### What stays the same

- Layout structure (header → accent → body → card → steps → divider → closing → footer)
- Colour palette (except accent line)
- Font stack and sizes
- Detail card pattern
- Plain text numbered steps
- Footer structure
- Sanitization logic
- Email client compatibility rules

---

## Implementation Pattern

### C# Builder Class

```csharp
public static class ConfirmationEmailBuilder
{
    public static (string subject, string body) Build(
        string firstName,
        string? lastName = null,
        string? email = null,
        string? companyName = null,
        string? inquiryType = null,
        string? platform = null,
        string? industry = null)
    {
        // Select template based on inquiryType
        // Build detail rows conditionally
        // Wrap in shared layout
        // Return (subject, body) tuple
    }
}
```

### Controller Integration

```csharp
var confirmation = ConfirmationEmailBuilder.Build(
    firstName: model.Name,
    lastName: surname,
    email: model.Email,
    companyName: companyName,
    inquiryType: inquiryType,
    platform: platform,
    industry: industry);

await _emailSender.SendEmailAsync(
    model.Email,
    confirmation.subject,
    confirmation.body,
    EmailDepartmentEnum.Ask);
```

### Key Helper Methods

| Method | Purpose |
|---|---|
| `WrapInLayout(...)` | Shared HTML shell (header, accent, body, footer) |
| `DetailRow(label, value)` | Single key-value row in the details card |
| `NumberedStep(number, text)` | Plain text step: `"1. Step text"` |
| `Sanitize(input)` | HTML entity encoding (`&`, `<`, `>`, `"`) |

---

## Checklist for New Product Emails

- [ ] Create `ConfirmationEmailBuilder.cs` in the product's `Services/Email/` directory
- [ ] Adapt logo URL, tagline, accent colour, and subject line prefix
- [ ] Define inquiry types and their detail card fields
- [ ] Define next steps for each inquiry type
- [ ] Define closing CTA text and link for each inquiry type
- [ ] Sanitize all user input before inserting into HTML
- [ ] Test in Outlook, Gmail, and Apple Mail
- [ ] Verify images load from public URLs
- [ ] Verify alt text renders readably when images are blocked
- [ ] Wire into the controller's email sending logic
