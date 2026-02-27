using System.ComponentModel.DataAnnotations;

namespace MV.DomainLayer.DTOs.Auth.Request
{
    public class ForgotPasswordRequest
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; } = null!;
    }
}
