using System.ComponentModel.DataAnnotations;

namespace MV.DomainLayer.DTOs.Address.Request
{
    public class UpdateAddressRequest
    {
        [Required(ErrorMessage = "Receiver name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Receiver name must be between 2 and 100 characters.")]
        public string ReceiverName { get; set; } = null!;

        [Required(ErrorMessage = "Phone number is required.")]
        [RegularExpression(@"^[0-9]{10,11}$", ErrorMessage = "Phone number must be 10-11 digits.")]
        public string Phone { get; set; } = null!;

        [Required(ErrorMessage = "Address line is required.")]
        [StringLength(255, ErrorMessage = "Address line must not exceed 255 characters.")]
        public string AddressLine { get; set; } = null!;

        [StringLength(100, ErrorMessage = "Ward must not exceed 100 characters.")]
        public string? Ward { get; set; }

        [Required(ErrorMessage = "District is required.")]
        [StringLength(100, ErrorMessage = "District must not exceed 100 characters.")]
        public string District { get; set; } = null!;

        [Required(ErrorMessage = "City is required.")]
        [StringLength(100, ErrorMessage = "City must not exceed 100 characters.")]
        public string City { get; set; } = null!;

        [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90.")]
        public decimal? Latitude { get; set; }

        [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180.")]
        public decimal? Longitude { get; set; }

        public bool IsDefault { get; set; } = false;
    }
}
