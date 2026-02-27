namespace MV.DomainLayer.DTOs.Auth.Response
{
    public class LoginResponse
    {
        public string AccessToken { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
        public int ExpiresIn { get; set; }
        public UserInfoResponse User { get; set; } = null!;
    }

    public class UserInfoResponse
    {
        public int UserId { get; set; }
        public string Username { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public string? Gender { get; set; }
        public int Role { get; set; }
        public bool IsEmailVerified { get; set; }
        public bool HasBodyProfile { get; set; }
    }
}
