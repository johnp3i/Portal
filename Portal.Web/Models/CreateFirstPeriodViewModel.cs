using System.ComponentModel.DataAnnotations;

namespace Portal.Web.Models;

public class CreateFirstPeriodViewModel : IValidatableObject
{
    [Required(ErrorMessage = "Start month is required.")]
    [Range(1, 12)]
    public int StartMonth { get; set; }

    [Required(ErrorMessage = "Start year is required.")]
    [Range(2000, 2100)]
    public int StartYear { get; set; }

    [Required(ErrorMessage = "End month is required.")]
    [Range(1, 12)]
    public int EndMonth { get; set; }

    [Required(ErrorMessage = "End year is required.")]
    [Range(2000, 2100)]
    public int EndYear { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartYear > 0 && StartMonth > 0 && EndYear > 0 && EndMonth > 0)
        {
            var start = new DateOnly(StartYear, StartMonth, 1);
            var end = new DateOnly(EndYear, EndMonth, DateTime.DaysInMonth(EndYear, EndMonth));

            if (end <= start)
            {
                yield return new ValidationResult(
                    "The end period must be after the start period.",
                    new[] { nameof(EndYear) });
            }
        }
    }
}
