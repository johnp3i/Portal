using Portal.Web.Configuration;
using Portal.Web.Models;
using Portal.Web.Services;
using Portal.Web.Services.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Stripe;
using System.Diagnostics;
using System.Runtime;
using System.Security.Claims;

namespace Portal.Web.Extensions
{
    public static class ServicesExtensions
    {
        public static void ConfigureWebsiteSettings(this IServiceCollection services, IConfiguration configuration)
        {

            services.Configure<WebsiteSettings>(configuration.GetSection(WebsiteSettings.SectionName));
            services.AddSingleton(s => s.GetRequiredService<IOptions<WebsiteSettings>>().Value);
        }
        public static void ConfigureEmailAccounts(this IServiceCollection services, IConfiguration configuration)
        {
            var emailAccounts = new List<EmailAccount>();
            configuration.GetSection("EmailAccounts").Bind(emailAccounts);
            services.AddSingleton(emailAccounts);
        }
        public static void ConfigureEmail(this IServiceCollection services)
        {
            services.AddTransient<IEmailSender, EmailSender>();
        }

        public static void ConfigureStripe(this IServiceCollection services, IConfiguration configuration)
        {
            var section = configuration.GetSection(StripeSettings.SectionName);
            services.Configure<StripeSettings>(section);

            var stripeSettings = section.Get<StripeSettings>();

            // Allow skipping validation in test environments via environment variable
            var skipValidation = Environment.GetEnvironmentVariable("SKIP_STRIPE_VALIDATION") == "true";

            if (skipValidation)
                return;

            var missingKeys = new List<string>();

            if (stripeSettings is null)
            {
                throw new InvalidOperationException(
                    $"Stripe configuration section '{StripeSettings.SectionName}' is missing. " +
                    "Ensure Stripe_BILI:SecretKey, Stripe_BILI:PublishableKey, and Stripe_BILI:WebhookSigningSecret are configured in User Secrets or environment variables.");
            }

            if (string.IsNullOrWhiteSpace(stripeSettings.SecretKey))
                missingKeys.Add("Stripe_BILI:SecretKey");

            if (string.IsNullOrWhiteSpace(stripeSettings.PublishableKey))
                missingKeys.Add("Stripe_BILI:PublishableKey");

            if (string.IsNullOrWhiteSpace(stripeSettings.WebhookSigningSecret))
                missingKeys.Add("Stripe_BILI:WebhookSigningSecret");

            if (missingKeys.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Required Stripe configuration values are missing or empty: {string.Join(", ", missingKeys)}. " +
                    "Configure these values in User Secrets for development.");
            }

            StripeConfiguration.ApiKey = stripeSettings.SecretKey;
        }
    }
}
