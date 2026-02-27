using System;
using System.Collections.Generic;

namespace MV.DomainLayer.Entities;

public partial class User
{
    public int Id { get; set; }

    public string Username { get; set; } = null!;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string Password { get; set; } = null!;

    public string? FullName { get; set; }

    public string? AvatarUrl { get; set; }

    public string? Gender { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public int Role { get; set; }

    public bool? IsActive { get; set; }

    public bool? IsEmailVerified { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

    public virtual ICollection<ChatAiHistory> ChatAiHistories { get; set; } = new List<ChatAiHistory>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<Order> OrderShippers { get; set; } = new List<Order>();

    public virtual ICollection<Order> OrderUsers { get; set; } = new List<Order>();

    public virtual ICollection<OtpCode> OtpCodes { get; set; } = new List<OtpCode>();

    public virtual ICollection<ProductReview> ProductReviews { get; set; } = new List<ProductReview>();

    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    public virtual ICollection<ShipperLocation> ShipperLocations { get; set; } = new List<ShipperLocation>();

    public virtual ICollection<UserAddress> UserAddresses { get; set; } = new List<UserAddress>();

    public virtual UserBodyProfile? UserBodyProfile { get; set; }

    public virtual ICollection<Wishlist> Wishlists { get; set; } = new List<Wishlist>();
}
