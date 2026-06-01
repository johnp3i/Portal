using System.ComponentModel.DataAnnotations;

namespace Portal.Web.Models;

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Email address is required")]
    [EmailAddress(ErrorMessage = "A valid email address is required")]
    [MaxLength(256)]
    public string Email { get; set; } = null!;
}
