namespace MV.DomainLayer.DTOs.User.Response
{
    public class AdminUserResponse
    {
        public int UserId { get; set; }
        public string Username { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? Phone { get; set; }
        public string FullName { get; set; } = null!;
        public string? Gender { get; set; }
        public int Role { get; set; }
        public bool IsActive { get; set; }
        public bool IsEmailVerified { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
