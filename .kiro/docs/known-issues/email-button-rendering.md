# Known Issue: Email CTA Button Rendering

## Problem

When creating HTML email templates with CTA (Call-to-Action) buttons, a simple `<a>` tag with inline styles does **not** render correctly across all email clients.

### What fails:

```html
<!-- This does NOT work reliably -->
<a href="{url}" style="display:inline-block;padding:14px 48px;background-color:#1e293b;color:#ffffff;border-radius:6px;">
    Accept Invitation
</a>
```

**Symptoms:**
- Button appears as plain text link (no background) in some clients
- Background color renders but border-radius is ignored
- Padding is stripped or inconsistent
- Button is left-aligned instead of centered

### Affected clients:
- Microsoft Outlook (desktop) — ignores most CSS on `<a>` tags
- Older Gmail rendering — strips `background-color` from inline elements
- Some webmail clients strip `display:inline-block`

---

## Solution: VML Conditional Comments + Table-Cell Approach

Use the **VML roundrect** pattern for Outlook and a properly styled `<a>` for all other clients, wrapped in conditional comments:

```html
<table role="presentation" cellpadding="0" cellspacing="0" align="center" style="margin:16px auto 32px;">
    <tr>
        <td align="center">
            <!--[if mso]>
            <v:roundrect xmlns:v="urn:schemas-microsoft-com:vml" xmlns:w="urn:schemas-microsoft-com:office:word"
                href="{acceptUrl}"
                style="height:48px;v-text-anchor:middle;width:240px;"
                arcsize="10%" strokecolor="#1e293b" fillcolor="#1e293b">
                <w:anchorlock/>
                <center style="color:#ffffff;font-family:sans-serif;font-size:15px;font-weight:bold;">
                    Accept Invitation
                </center>
            </v:roundrect>
            <![endif]-->
            <!--[if !mso]><!-->
            <a href="{acceptUrl}" target="_blank"
               style="display:inline-block;padding:14px 48px;background-color:#1e293b;color:#ffffff;font-size:15px;font-weight:700;text-decoration:none;border-radius:6px;letter-spacing:0.3px;">
                Accept Invitation
            </a>
            <!--<![endif]-->
        </td>
    </tr>
</table>
```

### How it works:

1. **`<!--[if mso]>`** — Only Outlook sees this. Uses VML (Vector Markup Language) to render a proper rounded rectangle with the link.
2. **`<!--[if !mso]><!-->`** — Everything except Outlook sees this. Uses the standard `<a>` tag with inline styles.
3. **Table wrapping** — The `<table>` with `align="center"` ensures the button is centered even when CSS centering fails.

### Key rules:
- `fillcolor` and `strokecolor` on VML control the button background and border in Outlook
- `arcsize="10%"` gives rounded corners in Outlook
- The `<a>` fallback uses `background-color` + `border-radius` for modern clients
- Always use `target="_blank"` on email links
- Never rely on `<td bgcolor>` alone — it doesn't support `border-radius` in most clients

---

## Where this applies in the codebase:

| File | Context |
|------|---------|
| `ChaplinPro/Services/InvitationService.cs` | `BuildInvitationEmailBody()` — team invitation emails |
| `Website/Areas/Chaplin/Services/InvitationEmailBuilder.cs` | Partner collaboration invitation emails (reference implementation) |
| Any future email template with a CTA button | Must follow the same VML pattern |

---

## Reference

The working implementation is in `Website/Areas/Chaplin/Services/InvitationEmailBuilder.cs` — always use that as the source of truth for email button styling.

---

*Documented: July 2026*
*Status: Resolved — use VML conditional comments for all email CTA buttons*
