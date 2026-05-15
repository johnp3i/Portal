# 3 Inventors — Identity Page Design Guide

Cross-platform design specification for login, registration, and account pages across all 3 Inventors products (MyChair, WorkforcePi, and future platforms). Follow this guide so every product's identity pages share a recognisable visual language while allowing per-product branding.

---

## 1. Page Structure

Every identity page follows a three-layer vertical layout:

```
┌─────────────────────────────────────┐
│  TOP BAR (brand + tagline)          │
├─────────────────────────────────────┤
│                                     │
│  CONTENT AREA                       │
│  (hero panel + card on desktop,     │
│   card only on mobile)              │
│                                     │
├─────────────────────────────────────┤
│  FOOTER (copyright line)            │
└─────────────────────────────────────┘
```

### Top Bar
- Full-width gradient bar using the product's primary blue
- Contains the product name (left-aligned to the content grid, not the viewport edge)
- Inner container: `max-width: 1200px`, centered, `padding: 18px 40px`
- On mobile: shows a tagline below the product name (hidden on desktop)

### Content Area
- Desktop: two-column grid (hero panel left, card right), vertically centered
- Mobile: single column, card only (hero panel hidden)

### Footer
- Centered copyright text: `© {year} {ProductName} · 3 Inventors`
- Muted, low-opacity text (`font-size: 12px`, `opacity: 0.6`)

---

## 2. Colour Palette

All products share the same base palette. Only the product name and tagline text change.

| Token | Value | Usage |
|-------|-------|-------|
| `--bg` | `#FBFDFF` | Page background start |
| `--bg2` | `#EEF4F8` | Page background end |
| `--surface` | `rgba(255,255,255,0.82)` | Card background (frosted glass) |
| `--line` | `rgba(13,94,166,0.10)` | Card border, dividers |
| `--text` | `#0B1B28` | Primary text |
| `--muted` | `#5E7385` | Secondary text, labels |
| `--blue` | `#0D5EA6` | Primary brand blue |
| `--blue-dark` | `#0B4E89` | Links, eyebrow text |
| `--cyan` | `#57B8E8` | Accent (used sparingly) |

### Top Bar Gradient
- Desktop: `linear-gradient(180deg, #1A6BB8 0%, #0D5EA6 100%)`
- Mobile: `linear-gradient(180deg, #0D5EA6 0%, #1A6BB8 60%, #2A7CC8 100%)` (slightly lighter to accommodate tagline)

### Page Background
Three layers, in order:
1. Radial gradient top-left: `rgba(13,94,166,0.08)` fading to transparent at 28%
2. Radial gradient top-right: `rgba(87,184,232,0.06)` fading to transparent at 22%
3. Linear gradient: `#FBFDFF` to `#EEF4F8` (top to bottom)

### Grid Overlay (subtle)
A faint grid pattern behind all content:
- Line colour: `rgba(13,94,166,0.05)`
- Grid size: `36px × 36px`
- Masked with a radial gradient so it fades at the edges

---

## 3. Typography

| Element | Font | Size | Weight | Tracking |
|---------|------|------|--------|----------|
| Product name (top bar) | Manrope | 20px | 800 | -0.03em |
| Tagline (mobile) | Inter | 14px | 400 | normal |
| Card eyebrow ("SECURE ACCESS") | Inter | 11px | 700 | 0.14em, uppercase |
| Card heading ("Sign in") | Manrope | 24px | 800 | -0.03em |
| Field labels | Inter | 12px | 700 | 0.02em |
| Field inputs | Inter | 14px | 400 | normal |
| Button text | Inter | 14px | 800 | normal |
| Links | Inter | 13px | 600 | normal |
| Footer | Inter | 12px | 400 | normal |

Font stack: `Inter, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif`
Heading font stack: `Manrope, Inter, sans-serif`

Load from Google Fonts:
```
Inter: 400, 500, 600, 700, 800
Manrope: 600, 700, 800
```

---

## 4. The Login Card

### Card Container
- Background: `rgba(255,255,255,0.82)` with `backdrop-filter: blur(16px)`
- Border: `1px solid rgba(13,94,166,0.10)`
- Border radius: `24px`
- Shadow: `0 18px 44px rgba(13,94,166,0.10)`
- Padding: `34px 30px 28px`
- Max width: `420px`
- Entrance animation: fade up (0.55s ease-out, 10px translateY)

### Card Header Row
A flex row at the top of every card with two elements:

**Left: Eyebrow label**
- Text: "SECURE ACCESS"
- Style: 11px, 700 weight, uppercase, letter-spacing 0.14em
- Colour: `--blue-dark` (#0B4E89)

**Right: Protected badge**
- Pill shape: `border-radius: 20px`, `padding: 5px 14px 5px 10px`
- Border: `1px solid rgba(13,94,166,0.15)`
- Background: `rgba(251,253,255,0.8)`
- Contains a 16×16 checkmark SVG icon + "Protected" text
- Colour: `--blue` (#0D5EA6)
- On mobile: slides in from right with a 0.4s fade animation

### Card Heading
- Text: "Sign in" (or "Create account", "Reset password", etc.)
- Manrope, 24px, 800 weight
- Colour: `--text` (#0B1B28)
- Margin: `4px 0 24px`

### Form Fields
- Label: 12px, 700 weight, colour `#23313D`, margin-bottom 8px
- Input: full width, `border-radius: 14px`, `padding: 14px 15px`
- Border: `1px solid rgba(13,94,166,0.12)`
- Background: `rgba(255,255,255,0.9)`
- Focus state: border `rgba(13,94,166,0.42)`, ring `0 0 0 4px rgba(13,94,166,0.10)`, background `#FFF`
- Placeholder colour: `#96A2AD`

### Primary Button
- Full width, `border-radius: 14px`, `padding: 14px 16px`
- Background: `linear-gradient(180deg, #1A6BB8 0%, #0D5EA6 100%)`
- Text: white, 14px, 800 weight
- Shadow: `0 14px 28px rgba(13,94,166,0.18)`
- Hover: `translateY(-1px)`
- Active: `translateY(0)`

### Secondary Links
- Colour: `--blue-dark` (#0B4E89)
- 13px, 600 weight
- Hover: underline

---

## 5. Interactive Background

A full-screen canvas behind all content with floating particles:

- **Particle count**: 38
- **Particle colour**: `rgba(13, 94, 166, opacity)` where opacity is 0.12–0.42
- **Particle size**: radius 1.2–3.4px
- **Drift speed**: 0.35 max velocity, dampened at 0.998
- **Connection lines**: drawn between particles within 140px, stroke `rgba(13,94,166, alpha)` where alpha = `(1 - distance/140) * 0.08`, line width 0.8px
- **Mouse interaction**: gentle repulsion within 180px radius, force factor 0.012
- **Accessibility**: disabled entirely when `prefers-reduced-motion: reduce`
- **Z-index**: 0 (behind all content), `pointer-events: none`

---

## 6. Desktop Layout (> 900px)

Two-column grid: `1.2fr 420px`, gap `32px`, max-width `1200px`, centered.

**Left column (hero panel)**:
- Product-specific marketing content
- Eyebrow label (uppercase, tracked, `--blue-dark`)
- Large heading (Manrope, `clamp(34px, 4.2vw, 54px)`, tight line-height 1.02)
- Body copy (Inter, 17px, `--muted`, line-height 1.75)
- Optional SVG illustration
- Entrance animation: fade up (0.55s, delayed 0.16s)

**Right column**: the login card (described above)

---

## 7. Mobile Layout (≤ 900px)

- Hero panel: hidden (`display: none`)
- Top bar: expanded gradient with tagline visible
- Card: full width (max 420px), `padding: 32px 20px 24px`
- Card border strengthened: `1px solid rgba(13,94,166,0.16)`
- Card shadow deepened: `0 20px 50px rgba(13,94,166,0.12), 0 2px 8px rgba(0,0,0,0.04)`
- Protected badge: entrance animation (slide from right, 0.4s)

---

## 8. Per-Product Customisation

Each product changes only these elements:

| Element | What to change |
|---------|---------------|
| Top bar product name | "MyChair", "WorkforcePi", "JDS", etc. |
| Top bar tagline | One-line description of the product |
| Footer copyright | `© {year} {ProductName} · 3 Inventors` |
| Hero panel content | Product-specific heading, copy, and illustration |
| Page title | `{PageTitle} - {ProductName}` |

Everything else — colours, typography, card design, form styling, button gradient, background, animations — stays identical.

---

## 9. Accessibility Requirements

- All form inputs have associated `<label>` elements
- Colour contrast meets WCAG AA (text on card surface, text on blue bar)
- Focus states are visible (blue ring on inputs, underline on links)
- Animations respect `prefers-reduced-motion: reduce`
- Interactive background canvas has `aria-hidden="true"`
- Card sections have `aria-label` attributes
- Form validation errors use `role="alert"`

---

## 10. Reference Implementation

The canonical implementation lives in the MyChair project:

- Layout: `Areas/Identity/Pages/_Layout.cshtml`
- CSS: `wwwroot/css/identity.css`
- Login page: `Areas/Identity/Pages/Account/Login.cshtml`

Copy the CSS and layout structure. Replace the product name, tagline, footer text, and hero panel content. The design system handles the rest.
