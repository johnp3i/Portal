# Locked Mockup Designs

The following mockup files are **design-locked** and must not be modified without explicit user approval. Implementation must match these designs exactly.

## Payment Reminders (locked 2 July 2026)

| File | Description |
|------|-------------|
| `payment-reminder-settings.html` | Settings → Payment Reminders configuration page (schedule table with per-tier toggles, suppression rules, email preview tabs) |
| `payment-reminder-emails.html` | Three escalation tier email templates (Friendly/Firm/Formal) using existing email styling |

### Key Design Decisions (locked)

- Per-tier enable/disable toggles in the schedule table (Friendly ON by default, Firm and Formal OFF)
- Disabled tiers shown greyed out (opacity 0.5) but still configurable
- Email accent line colour matches tier: Blue (Friendly), Amber (Firm), Red (Formal)
- Email CTA buttons: "View Invoice" (blue), "Pay Now" (amber), "Settle Invoice" (red)
- Suppression rules in a separate card with days input and system-wide toggle
- Email preview as tabbed interface within the settings page
- Footer shows business name + "Powered by 3 Inventors"
