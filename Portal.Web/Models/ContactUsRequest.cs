using System.ComponentModel.DataAnnotations;

namespace Portal.Web.Models;

public class ContactUsRequest
{
    public string InquiryType { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    [Required]
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    public string? Telephone { get; set; }
    public string? Industry { get; set; }
    public string? Website { get; set; }        // Honeypot field
    public string? RecaptchaToken { get; set; }
}
