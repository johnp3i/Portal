@echo off
REM ============================================================
REM  Stripe Environment Variables — BILI Platform
REM  Run this script as Administrator on the live server.
REM  Sets system-level environment variables that persist
REM  across reboots and are available to IIS / Windows Services.
REM
REM  Variable naming: Stripe_BILI__<Key>
REM  This allows multiple platforms (BILI, JDS, etc.) to coexist
REM  on the same server with separate Stripe accounts.
REM ============================================================

echo.
echo Setting Stripe environment variables for BILI platform...
echo.

REM --- BILI Stripe Keys (REPLACE with your live keys) ---
setx Stripe_BILI__SecretKey "sk_live_REPLACE_WITH_BILI_SECRET_KEY" /M
setx Stripe_BILI__PublishableKey "pk_live_REPLACE_WITH_BILI_PUBLISHABLE_KEY" /M
setx Stripe_BILI__WebhookSigningSecret "whsec_REPLACE_WITH_BILI_WEBHOOK_SECRET" /M

echo.
echo ============================================================
echo  Done. Stripe variables set for BILI platform.
echo  
echo  Variables created:
echo    Stripe_BILI__SecretKey
echo    Stripe_BILI__PublishableKey
echo    Stripe_BILI__WebhookSigningSecret
echo  
echo  IMPORTANT:
echo    1. Restart IIS (iisreset) or the application pool
echo       for the new variables to take effect.
echo    2. Verify with: set Stripe_BILI
echo ============================================================
echo.

pause
