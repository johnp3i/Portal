using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Portal.Web.Services.Email
{
    public class EmailAccount
    {
        public string? SenderEmail { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public EmailDepartmentEnum Department { get; set; }

    }
}
