namespace MV.DomainLayer.DTOs.User.Response
{
    public class UserProfileResponse
    {
        public int UserId { get; set; }
        public string Username { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? Phone { get; set; }
        public string FullName { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public string? Gender { get; set; }
        public DateOnly? DateOfBirth { get; set; }
        public int Role { get; set; }
        public bool IsEmailVerified { get; set; }
        public bool HasBodyProfile { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
