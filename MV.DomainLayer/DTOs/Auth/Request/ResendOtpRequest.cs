using System.ComponentModel.DataAnnotations;

namespace MV.DomainLayer.DTOs.Auth.Request
{
    public class ResendOtpRequest
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "OTP type is required.")]
        [RegularExpression(@"^(VERIFY_EMAIL|RESET_PASSWORD)$", ErrorMessage = "Type must be VERIFY_EMAIL or RESET_PASSWORD.")]
        public string Type { get; set; } = null!;
    }
}
