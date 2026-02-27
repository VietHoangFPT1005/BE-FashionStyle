namespace MV.DomainLayer.DTOs.Auth.Response
{
    public class RegisterResponse
    {
        public int UserId { get; set; }
        public string Email { get; set; } = null!;
        public bool RequiresVerification { get; set; } = true;
    }
}
