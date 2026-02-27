using System.ComponentModel.DataAnnotations;

namespace MV.DomainLayer.DTOs.Auth.Request
{
    public class LoginRequest
    {
        [Required(ErrorMessage = "Email or phone number is required.")]
        public string EmailOrPhone { get; set; } = null!;

        [Required(ErrorMessage = "Password is required.")]
        public string Password { get; set; } = null!;
    }
}
