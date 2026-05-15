using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Portal.Web.Models
{
    public class WebsiteSettings
    {
        public const string SectionName = nameof(WebsiteSettings);
        public EmailServer EmailServer { get; set; }
        public byte DefaultCountryID { get; set; }
    }

    public class EmailServer
    {
        public string SmtpAddress { get; set; }
        public int SmtpPort { get; set; }
        public bool EnableSSL { get; set; }
    }
}
