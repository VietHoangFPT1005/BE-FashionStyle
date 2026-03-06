using System.ComponentModel.DataAnnotations;

namespace MV.DomainLayer.DTOs.Auth.Request
{
    public class ChangePasswordRequest
    {
        [Required(ErrorMessage = "Current password is required.")]
        public string CurrentPassword { get; set; } = null!;

        [Required(ErrorMessage = "New password is required.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "New password must be at least 6 characters.")]
        public string NewPassword { get; set; } = null!;

        [Required(ErrorMessage = "OTP code is required.")]
        [RegularExpression(@"^[0-9]{6}$", ErrorMessage = "OTP code must be exactly 6 digits.")]
        public string OtpCode { get; set; } = null!;
    }
}
