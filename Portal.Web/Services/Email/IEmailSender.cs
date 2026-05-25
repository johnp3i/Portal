using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Portal.Web.Services.Email
{
    public interface IEmailSender
    {
        Task SendEmailAsync(string email, string subject, string message, EmailDepartmentEnum department);
        Task SendEmailWithAttachmentAsync(string email, string subject, string message, EmailDepartmentEnum department, byte[] attachmentBytes, string attachmentFilename, string attachmentContentType);
    }
}
