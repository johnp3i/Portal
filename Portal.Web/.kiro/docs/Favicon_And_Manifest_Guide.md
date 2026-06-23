# Favicon & Web Manifest Guide

## Overview

This document covers how favicons, homescreen shortcut icons, and the web app manifest are configured in the Portal platform.

---

## File Location

All icon files live in:

```
Portal.Web/wwwroot/images/favicon/
```

The web manifest file lives at:

```
Portal.Web/wwwroot/site.webmanifest
```

---

## Icon Files

| File | Size | Purpose |
|------|------|---------|
| `favicon.ico` | Multi-size | Browser tab icon (legacy) |
| `favicon-16x16.png` | 16×16 | Browser tab icon (modern) |
| `favicon-32x32.png` | 32×32 | Browser tab icon (retina) |
| `favicon-96x96.png` | 96×96 | Desktop shortcut |
| `android-icon-36x36.png` | 36×36 | Android ldpi |
| `android-icon-48x48.png` | 48×48 | Android mdpi |
| `android-icon-72x72.png` | 72×72 | Android hdpi |
| `android-icon-96x96.png` | 96×96 | Android xhdpi |
| `android-icon-144x144.png` | 144×144 | Android xxhdpi |
| `android-icon-192x192.png` | 192×192 | Android xxxhdpi / Chrome shortcut |
| `apple-touch-icon-180x180.png` | 180×180 | iOS homescreen (primary) |
| `apple-touch-icon-152x152.png` | 152×152 | iPad |
| `apple-touch-icon-144x144.png` | 144×144 | iPad retina (older) |
| `ms-icon-144x144.png` | 144×144 | Windows tile |

---

## Web Manifest (`site.webmanifest`)

The manifest tells browsers how to display the app when installed as a shortcut or PWA.

```json
{
  "name": "Portal",
  "short_name": "Portal",
  "description": "Operational Intelligence Platform",
  "start_url": "/",
  "display": "standalone",
  "background_color": "#F7FAFC",
  "theme_color": "#0D5EA6",
  "icons": [...]
}
```

### Key Fields

| Field | Value | Purpose |
|-------|-------|---------|
| `name` | "Portal" | Full app name (splash screen) |
| `short_name` | "Portal" | Below homescreen icon |
| `display` | "standalone" | App-like (no browser chrome) |
| `background_color` | #F7FAFC | Splash screen background |
| `theme_color` | #0D5EA6 | Status bar / address bar colour |
| `start_url` | "/" | What opens when shortcut is tapped |

### Icon Purpose

Each icon in the manifest includes `"purpose": "any maskable"`:
- **any** — standard icon rendering
- **maskable** — allows Android to crop the icon into circles, rounded squares, etc. without cutting off content

---

## Where the Manifest Link Is Included

The `<link rel="manifest">` tag is in:

- `Views/Shared/_Layout.cshtml` (main app)
- `Views/Shared/_IdentityLayout.cshtml` (registration pages)
- `Views/Account/Login.cshtml`
- `Views/Account/AccessDenied.cshtml`
- `Views/Landing/Index.cshtml`
- `Views/Legal/CookiesPolicy.cshtml`
- `Views/Legal/PrivacyPolicy.cshtml`
- `Views/Legal/TermsAndConditions.cshtml`
- `Views/Legal/TermsOfUse.cshtml`

**NOT included in:** `Invoice/Snapshot.cshtml`, `Proposal/Snapshot.cshtml` (standalone document views).

---

## Cache Busting

The manifest link uses a version query string:

```html
<link rel="manifest" href="/site.webmanifest?v=2" />
```

**When you update icons or the manifest, bump the version number** (e.g., `?v=3`). This forces browsers to re-fetch the manifest instead of serving a cached version.

### Files to Update When Bumping Version

All 9 files listed above that contain the `<link rel="manifest">` tag.

---

## How Each Platform Picks the Shortcut Icon

| Platform | Source | Recommended Size |
|----------|--------|-----------------|
| Android (Chrome) | `site.webmanifest` icons array | 192×192 (required), 512×512 (recommended) |
| iOS (Safari) | `<link rel="apple-touch-icon">` | 180×180 |
| Windows | `<meta name="msapplication-TileImage">` | 144×144 |
| Desktop browsers | `<link rel="icon">` | 32×32 or favicon.ico |

---

## Updating Icons

When replacing icons:

1. Replace the files in `wwwroot/images/favicon/`
2. Bump the cache-bust version in all manifest links (`?v=2` → `?v=3`)
3. Publish
4. Users must **delete and re-add** existing shortcuts to see the new icon (OS limitation)

### Generating New Icons

Use [RealFaviconGenerator](https://realfavicongenerator.net/) or [Favicon.io](https://favicon.io/) to generate all sizes from a single source image. Recommended source: at least 512×512 PNG with transparent background.

---

## Missing (Future Enhancement)

- **512×512 icon** — Chrome recommends this for PWA install banners. Currently the largest Android icon is 192×192. Adding a 512×512 would improve quality on high-DPI devices and enable the Chrome "Install App" prompt.

To add: create `android-icon-512x512.png`, place it in the favicon folder, and add an entry to `site.webmanifest`:

```json
{
  "src": "/images/favicon/android-icon-512x512.png",
  "sizes": "512x512",
  "type": "image/png",
  "purpose": "any maskable"
}
```
