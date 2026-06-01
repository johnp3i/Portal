using System.ComponentModel.DataAnnotations;

namespace Portal.Web.Models;

public class RegisterViewModel
{
    [Required(ErrorMessage = "First name is required")]
    [MaxLength(100)]
    public string FirstName { get; set; } = null!;

    [Required(ErrorMessage = "Last name is required")]
    [MaxLength(100)]
    public string LastName { get; set; } = null!;

    [Required(ErrorMessage = "Email address is required")]
    [EmailAddress(ErrorMessage = "A valid email address is required")]
    [MaxLength(256)]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Password is required")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
    public string Password { get; set; } = null!;

    [Required(ErrorMessage = "Please confirm your password")]
    [Compare(nameof(Password), ErrorMessage = "Passwords must match")]
    public string ConfirmPassword { get; set; } = null!;

    [Required(ErrorMessage = "Please select a plan")]
    public int? SelectedPlanId { get; set; }

    // For display
    public List<PlanDisplayModel>? AvailablePlans { get; set; }
    public PlanDisplayModel? PreSelectedPlan { get; set; }
}
