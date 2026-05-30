# Contact Modal Implementation Guide

## Purpose

This document describes how to implement a context-aware contact modal on a 3 Inventors landing page. The modal captures visitor inquiries with contextual metadata (inquiry type, industry, pricing plan) and integrates with an existing ASP.NET Core MVC `HomeController.ContactUs` action.

This is a generic guide applicable to any 3 Inventors product landing page.

---

## 1. Overview

The landing page has multiple CTA buttons ("Request Demo", "Talk to us", "Tailor a plan") that all open the same modal form. Each button passes context so the backend knows what the visitor is interested in.

The modal dynamically updates its title, subtitle, and badge based on the trigger button. After submission, a toast notification confirms the request was sent.

---

## 2. Inquiry Types

Each CTA button carries a `data-inquiry` value via an `onclick` handler:

| Button Text | Location | inquiryType Value |
|-------------|----------|-------------------|
| Request Demo | Header, Hero, Footer | `Demo Request` |
| Talk to us | Core pricing card | `Pricing - Core` |
| Talk to us | Enhanced pricing card | `Pricing - Enhanced` |
| Tailor a plan | Enterprise pricing card | `Pricing - Enterprise` |
| Talk to us | FAQ section, general | `General Inquiry` |

### Button HTML Pattern

```html
<a href="javascript:void(0)"
   onclick="openContactModal('Demo Request')"
   class="brand-primary-btn ...">
   Request Demo
</a>
```

For pricing buttons, pass the plan name:

```html
<a href="javascript:void(0)"
   onclick="openContactModal('Pricing - Core')"
   class="...">
   Talk to us
</a>
```

---

## 3. Modal Structure

The modal is a fixed overlay with a centered card. It contains:

- A hidden `inquiryType` field (auto-set by the trigger button)
- Dynamic title, subtitle, and badge text
- Form fields: Company Name, First Name, Last Name, Email, Telephone, Industry dropdown
- Anti-forgery token for ASP.NET Core
- Submit button
- Close button (X), backdrop click, and Escape key to dismiss

### Modal HTML

```html
<!-- Contact Modal Overlay -->
<div id="contactModal"
     class="fixed inset-0 z-[100] hidden items-center justify-center
            bg-slate-950/60 backdrop-blur-sm">
  <div class="relative mx-4 w-full max-w-lg rounded-3xl border
              border-slate-200 bg-white p-8
              shadow-[0_20px_60px_rgba(0,85,151,0.18)]">

    <!-- Close button -->
    <button onclick="closeContactModal()"
            class="absolute right-4 top-4 flex h-8 w-8 items-center
                   justify-center rounded-full text-slate-400
                   transition hover:bg-slate-100 hover:text-slate-700">
      <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20"
           viewBox="0 0 24 24" fill="none" stroke="currentColor"
           stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
        <line x1="18" y1="6" x2="6" y2="18"></line>
        <line x1="6" y1="6" x2="18" y2="18"></line>
      </svg>
    </button>

    <!-- Modal header (dynamic) -->
    <div class="brand-badge inline-flex items-center gap-2 rounded-full
                border px-3 py-1.5 text-xs font-medium shadow-sm">
      <span class="inline-block h-2 w-2 rounded-full bg-sky-500"></span>
      <span id="modalBadge">Contact Us</span>
    </div>
    <h2 id="modalTitle"
        class="mt-4 text-2xl font-bold tracking-tight text-slate-950">
      Request a Demo
    </h2>
    <p id="modalSubtitle"
       class="mt-2 text-sm text-slate-500">
      We'll get back to you within 24 hours.
    </p>

    <!-- Form -->
    <form id="contactForm" class="mt-6 space-y-3">
      @Html.AntiForgeryToken()
      <input type="hidden" id="inquiryType"
             name="inquiryType" value="Demo Request" />

      <input type="text" name="companyName"
             placeholder="Company Name"
             class="w-full rounded-xl border border-slate-200
                    bg-slate-50 px-4 py-3 text-sm ..." />

      <div class="grid grid-cols-2 gap-3">
        <input type="text" name="name" placeholder="First Name *"
               required class="..." />
        <input type="text" name="surname" placeholder="Last Name"
               class="..." />
      </div>

      <input type="email" name="email" placeholder="Email *"
             required class="..." />

      <input type="tel" name="telephone" placeholder="Telephone"
             class="..." />

      <select name="industry" class="...">
        <option value="">Select your industry (optional)</option>
        <option value="HORECA">HORECA (Hotels, Restaurants, Cafés)</option>
        <option value="Retail">Retail</option>
        <option value="Warehouse / Logistics">Warehouse / Logistics</option>
        <option value="Manufacturing">Manufacturing</option>
        <option value="Services">Services</option>
        <option value="Office">Office</option>
        <option value="Gym / Fitness">Gym / Fitness</option>
        <option value="Other">Other</option>
      </select>

      <button type="submit" id="contactSubmitBtn"
              class="brand-primary-btn mt-2 w-full rounded-xl px-6
                     py-3.5 text-sm font-semibold text-white">
        Send Request
      </button>
    </form>
    <p class="mt-4 text-center text-xs text-slate-400">
      We avoid Spam &amp; Junk Emails.
    </p>
  </div>
</div>
```

---

## 4. Success Toast

A fixed-position notification shown after successful submission:

```html
<div id="successToast"
     class="fixed bottom-6 right-6 z-[110] hidden max-w-sm rounded-2xl
            border border-sky-200 bg-white p-5
            shadow-[0_12px_30px_rgba(0,85,151,0.12)]">
  <div class="flex items-start gap-3">
    <div class="flex h-8 w-8 flex-shrink-0 items-center justify-center
                rounded-full bg-sky-100 text-sky-700">✓</div>
    <div>
      <p class="text-sm font-semibold text-slate-950">Request Sent</p>
      <p class="mt-1 text-sm text-slate-600">
        Your request has been sent. We will get in touch within 24 hours.
      </p>
    </div>
    <button onclick="document.getElementById('successToast')
                     .classList.add('hidden')"
            class="ml-2 flex-shrink-0 text-slate-400
                   hover:text-slate-700">✕</button>
  </div>
</div>
```

Auto-hides after 6 seconds.

---

## 5. JavaScript

All JS is vanilla — no jQuery required on the marketing site.

```javascript
// Modal title/subtitle configuration per inquiry type
const modalTitles = {
    'Demo Request': {
        badge: 'Demo Request',
        title: 'Request a Demo',
        subtitle: 'See the platform in action. We\'ll walk you through it.'
    },
    'General Inquiry': {
        badge: 'Contact Us',
        title: 'Talk to Us',
        subtitle: 'Have a question? We\'ll get back to you within 24 hours.'
    },
    'Pricing - Core': {
        badge: 'Core Plan',
        title: 'Interested in the Core plan?',
        subtitle: 'Tell us about your team and we\'ll help you get started.'
    },
    'Pricing - Enhanced': {
        badge: 'Enhanced Plan',
        title: 'Interested in the Enhanced plan?',
        subtitle: 'Tell us about your team and we\'ll help you get started.'
    },
    'Pricing - Enterprise': {
        badge: 'Enterprise Plan',
        title: 'Let\'s tailor a plan for you',
        subtitle: 'Tell us about your organisation and we\'ll build a custom proposal.'
    }
};

// Open modal with context
function openContactModal(inquiryType) {
    const config = modalTitles[inquiryType] || modalTitles['General Inquiry'];
    document.getElementById('modalBadge').textContent = config.badge;
    document.getElementById('modalTitle').textContent = config.title;
    document.getElementById('modalSubtitle').textContent = config.subtitle;
    document.getElementById('inquiryType').value = inquiryType;
    const modal = document.getElementById('contactModal');
    modal.classList.remove('hidden');
    modal.classList.add('flex');
}

// Close modal
function closeContactModal() {
    const modal = document.getElementById('contactModal');
    modal.classList.add('hidden');
    modal.classList.remove('flex');
}

// Close on backdrop click
document.getElementById('contactModal').addEventListener('click', function(e) {
    if (e.target === this) closeContactModal();
});

// Close on Escape key
document.addEventListener('keydown', function(e) {
    if (e.key === 'Escape') closeContactModal();
});

// Form submission via fetch
document.getElementById('contactForm').addEventListener('submit', async function(e) {
    e.preventDefault();
    const btn = document.getElementById('contactSubmitBtn');
    btn.disabled = true;
    btn.textContent = 'Sending...';

    const formData = new FormData(this);
    const token = formData.get('__RequestVerificationToken');

    try {
        const response = await fetch(contactUsUrl, {  // contactUsUrl = @Url.Action("ContactUs", "Home")
            method: 'POST',
            headers: { 'RequestVerificationToken': token },
            body: new URLSearchParams(formData)
        });

        if (response.ok) {
            this.reset();
            closeContactModal();
            showSuccessToast();
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

// Toast notification
function showSuccessToast() {
    const toast = document.getElementById('successToast');
    toast.classList.remove('hidden');
    setTimeout(() => toast.classList.add('hidden'), 6000);
}
```

In the Razor view, set the URL variable before the script:

```html
@section Scripts {
<script>
    var contactUsUrl = '@Url.Action("ContactUs", "Home")';
    // ... rest of the JS above
</script>
}
```

---

## 6. Backend Integration

### Controller Action

The existing `HomeController.ContactUs` action needs two new optional parameters:

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> ContactUs(
    string name, string surname, string email,
    string companyName, string telephone,
    string? inquiryType = null, string? industry = null)
```

These are optional so the existing form (if any) continues to work without changes.

### Email Format Helper

Update the `FormatMessage` method to include the new fields:

```csharp
public static string FormatMessage(
    string email, string name, string surname,
    string telephone, string companyName,
    string? inquiryType = null, string? industry = null)
{
    string fmessage = "<html><head><title></title></head><body>";
    fmessage += "<table border=0 width=95% cellpadding=0 cellspacing=0>";

    if (!string.IsNullOrWhiteSpace(inquiryType))
    {
        fmessage += "<tr>";
        fmessage += "<td class='text-right'>Inquiry Type: </td>";
        fmessage += "<td class='text-left'><b>" + inquiryType + "</b></td>";
        fmessage += "</tr>";
    }

    // ... existing fields (email, name, surname, telephone, companyName) ...

    if (!string.IsNullOrWhiteSpace(industry))
    {
        fmessage += "<tr>";
        fmessage += "<td class='text-right'>Industry: </td>";
        fmessage += "<td class='text-left'>" + industry + "</td>";
        fmessage += "</tr>";
    }

    fmessage += "</table></body></html>";
    return fmessage;
}
```

### Email Subject

Use the inquiry type in the email subject for easier triage:

```csharp
var emailSubject = !string.IsNullOrWhiteSpace(inquiryType)
    ? $"ProductName - {inquiryType}"
    : "ProductName - Request";
```

### Contact Form API Log (if integrated)

Include inquiry type and industry in the API message:

```csharp
var apiMessage = $"Name: {name}\nSurname: {surname}\nCompany: {companyName}"
    + $"\nTelephone: {telephone}\nInquiry: {inquiryType ?? "N/A"}"
    + $"\nIndustry: {industry ?? "N/A"}";
```

---

## 7. Industry Dropdown Options

The standard industry list for 3 Inventors products:

| Value | Display Text |
|-------|-------------|
| (empty) | Select your industry (optional) |
| HORECA | HORECA (Hotels, Restaurants, Cafés) |
| Retail | Retail |
| Warehouse / Logistics | Warehouse / Logistics |
| Manufacturing | Manufacturing |
| Services | Services |
| Office | Office |
| Gym / Fitness | Gym / Fitness |
| Other | Other |

Adapt the list per product if needed, but keep the `<option value="">` default.

---

## 8. Customisation Checklist

When applying this to a new product landing page:

1. Replace product name in `modalTitles` subtitles (e.g., "See WorkforcePi in action" → "See ProductName in action")
2. Update the `contactUsUrl` to point to the correct controller action
3. Adjust the inquiry type values if the pricing plans have different names
4. Update the industry dropdown if the product targets different sectors
5. Ensure the `@Html.AntiForgeryToken()` is present in the form
6. Verify the `HomeController.ContactUs` action accepts `inquiryType` and `industry` parameters
7. Update the email subject prefix to match the product name
8. Style the modal and toast to match the product's brand CSS variables

---

## 9. CSS Dependencies

The modal uses these brand classes (must be defined in the page layout):

```css
.brand-badge {
    background: linear-gradient(180deg, #F3FBFF 0%, #EAF7FD 100%);
    border-color: rgba(0,188,242,0.28);
    color: var(--brand-blue);
}

.brand-primary-btn {
    background: linear-gradient(180deg, #0079C9 0%, #005597 100%);
    box-shadow: 0 14px 30px rgba(0,85,151,0.24);
}

.brand-primary-btn:hover {
    background: linear-gradient(180deg, #006CB4 0%, #00467D 100%);
}
```

All form inputs use Tailwind utility classes directly — no custom CSS needed for inputs.
