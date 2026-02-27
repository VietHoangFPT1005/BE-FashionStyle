using System.ComponentModel.DataAnnotations;

namespace MV.DomainLayer.DTOs.User.Request
{
    public class UpdateProfileRequest
    {
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 100 characters.")]
        public string? FullName { get; set; }

        [RegularExpression(@"^[0-9]{10,11}$", ErrorMessage = "Phone number must be 10-11 digits.")]
        public string? Phone { get; set; }

        [RegularExpression(@"^(MALE|FEMALE|OTHER)$", ErrorMessage = "Gender must be MALE, FEMALE, or OTHER.")]
        public string? Gender { get; set; }

        public DateOnly? DateOfBirth { get; set; }

        [StringLength(500, ErrorMessage = "Avatar URL must not exceed 500 characters.")]
        [Url(ErrorMessage = "Avatar URL must be a valid URL.")]
        public string? AvatarUrl { get; set; }
    }
}
