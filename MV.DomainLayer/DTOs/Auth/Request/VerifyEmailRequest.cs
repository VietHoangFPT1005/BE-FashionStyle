using System.ComponentModel.DataAnnotations;

namespace MV.DomainLayer.DTOs.Auth.Request
{
    public class VerifyEmailRequest
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "OTP code is required.")]
        [RegularExpression(@"^[0-9]{6}$", ErrorMessage = "OTP code must be exactly 6 digits.")]
        public string OtpCode { get; set; } = null!;
    }
}
