using Portal.Web.Models;
using Portal.Web.Services;
using Portal.Web.Services.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
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
    }
}
