using System.ComponentModel.DataAnnotations;

namespace Portal.Web.Models
{
    public class CustomerFormViewModel
    {
        [Required(ErrorMessage = "Customer name is required")]
        [MaxLength(200)]
        public string Name { get; set; } = null!;

        [MaxLength(200)]
        public string? ContactPerson { get; set; }

        [MaxLength(200)]
        [EmailAddress(ErrorMessage = "Email address is not in a valid format")]
        public string? Email { get; set; }

        [MaxLength(30)]
        public string? TelephoneNumber { get; set; }

        [MaxLength(30)]
        public string? MobileNumber { get; set; }

        [MaxLength(200)]
        public string? AddressLine1 { get; set; }

        [MaxLength(200)]
        public string? AddressLine2 { get; set; }

        [MaxLength(100)]
        public string? City { get; set; }

        [MaxLength(20)]
        public string? PostalCode { get; set; }

        [MaxLength(100)]
        public string? Country { get; set; }
    }
}
