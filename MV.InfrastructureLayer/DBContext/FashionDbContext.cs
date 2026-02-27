using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MV.DomainLayer.Entities;
using System;
using System.Collections.Generic;

namespace MV.InfrastructureLayer.DBContext;

public partial class FashionDbContext : DbContext
{
    public FashionDbContext()
    {
    }

    public FashionDbContext(DbContextOptions<FashionDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<CartItem> CartItems { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<ChatAiHistory> ChatAiHistories { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderItem> OrderItems { get; set; }

    public virtual DbSet<OtpCode> OtpCodes { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<ProductImage> ProductImages { get; set; }

    public virtual DbSet<ProductReview> ProductReviews { get; set; }

    public virtual DbSet<ProductVariant> ProductVariants { get; set; }

    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

    public virtual DbSet<ShipperLocation> ShipperLocations { get; set; }

    public virtual DbSet<SizeGuide> SizeGuides { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserAddress> UserAddresses { get; set; }

    public virtual DbSet<UserBodyProfile> UserBodyProfiles { get; set; }

    public virtual DbSet<Voucher> Vouchers { get; set; }

    public virtual DbSet<Wishlist> Wishlists { get; set; }

    private string GetConnectionString()
    {
        IConfiguration config = new ConfigurationBuilder()
             .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", true, true)
                    .Build();
        var strConn = config["ConnectionStrings:DefaultConnection"];

        return strConn!;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseNpgsql(GetConnectionString());

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("CartItems_pkey");

            entity.HasIndex(e => new { e.UserId, e.ProductVariantId }, "CartItems_UserId_ProductVariantId_key").IsUnique();

            entity.HasIndex(e => e.UserId, "Idx_CartItems_User");

            entity.Property(e => e.AddedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Quantity).HasDefaultValue(1);

            entity.HasOne(d => d.ProductVariant).WithMany(p => p.CartItems)
                .HasForeignKey(d => d.ProductVariantId)
                .HasConstraintName("CartItems_ProductVariantId_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.CartItems)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("CartItems_UserId_fkey");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Categories_pkey");

            entity.HasIndex(e => e.Slug, "Categories_Slug_key").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.ImageUrl).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Slug).HasMaxLength(100);
            entity.Property(e => e.SortOrder).HasDefaultValue(0);

            entity.HasOne(d => d.Parent).WithMany(p => p.InverseParent)
                .HasForeignKey(d => d.ParentId)
                .HasConstraintName("Categories_ParentId_fkey");
        });

        modelBuilder.Entity<ChatAiHistory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ChatAiHistory_pkey");

            entity.ToTable("ChatAiHistory");

            entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "Idx_ChatAiHistory_User").IsDescending(false, true);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Role).HasMaxLength(10);
            entity.Property(e => e.SessionId).HasMaxLength(50);

            entity.HasOne(d => d.User).WithMany(p => p.ChatAiHistories)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("ChatAiHistory_UserId_fkey");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Notifications_pkey");

            entity.HasIndex(e => new { e.UserId, e.IsRead, e.CreatedAt }, "Idx_Notifications_User").IsDescending(false, false, true);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Data).HasColumnType("jsonb");
            entity.Property(e => e.IsRead).HasDefaultValue(false);
            entity.Property(e => e.Title).HasMaxLength(255);
            entity.Property(e => e.Type).HasMaxLength(50);

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("Notifications_UserId_fkey");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Orders_pkey");

            entity.HasIndex(e => e.CreatedAt, "Idx_Orders_Created").IsDescending();

            entity.HasIndex(e => e.ShipperId, "Idx_Orders_Shipper");

            entity.HasIndex(e => e.Status, "Idx_Orders_Status");

            entity.HasIndex(e => e.UserId, "Idx_Orders_User");

            entity.HasIndex(e => e.OrderCode, "Orders_OrderCode_key").IsUnique();

            entity.Property(e => e.CancelReason).HasMaxLength(500);
            entity.Property(e => e.CancelledAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.ConfirmedAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.DeliveredAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.DeliveryAttempts).HasDefaultValue(0);
            entity.Property(e => e.Discount)
                .HasPrecision(15, 2)
                .HasDefaultValueSql("0");
            entity.Property(e => e.OrderCode).HasMaxLength(50);
            entity.Property(e => e.ShippedAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.ShippingCity).HasMaxLength(100);
            entity.Property(e => e.ShippingDistrict).HasMaxLength(100);
            entity.Property(e => e.ShippingFee)
                .HasPrecision(15, 2)
                .HasDefaultValueSql("30000");
            entity.Property(e => e.ShippingLatitude).HasPrecision(10, 7);
            entity.Property(e => e.ShippingLongitude).HasPrecision(10, 7);
            entity.Property(e => e.ShippingName).HasMaxLength(100);
            entity.Property(e => e.ShippingPhone).HasMaxLength(20);
            entity.Property(e => e.ShippingWard).HasMaxLength(100);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValueSql("'PENDING'::character varying");
            entity.Property(e => e.Subtotal).HasPrecision(15, 2);
            entity.Property(e => e.Total).HasPrecision(15, 2);

            entity.HasOne(d => d.Shipper).WithMany(p => p.OrderShippers)
                .HasForeignKey(d => d.ShipperId)
                .HasConstraintName("Orders_ShipperId_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.OrderUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Orders_UserId_fkey");

            entity.HasOne(d => d.Voucher).WithMany(p => p.Orders)
                .HasForeignKey(d => d.VoucherId)
                .HasConstraintName("Orders_VoucherId_fkey");
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("OrderItems_pkey");

            entity.HasIndex(e => e.OrderId, "Idx_OrderItems_Order");

            entity.Property(e => e.Color).HasMaxLength(50);
            entity.Property(e => e.Price).HasPrecision(15, 2);
            entity.Property(e => e.ProductImage).HasMaxLength(500);
            entity.Property(e => e.ProductName).HasMaxLength(255);
            entity.Property(e => e.Size).HasMaxLength(20);
            entity.Property(e => e.Subtotal).HasPrecision(15, 2);

            entity.HasOne(d => d.Order).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("OrderItems_OrderId_fkey");

            entity.HasOne(d => d.ProductVariant).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.ProductVariantId)
                .HasConstraintName("OrderItems_ProductVariantId_fkey");
        });

        modelBuilder.Entity<OtpCode>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("OtpCodes_pkey");

            entity.HasIndex(e => new { e.Email, e.Type, e.IsUsed }, "Idx_OtpCodes_Email");

            entity.HasIndex(e => new { e.UserId, e.Type, e.IsUsed }, "Idx_OtpCodes_User");

            entity.Property(e => e.Code).HasMaxLength(6);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.ExpiredAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.IsUsed).HasDefaultValue(false);
            entity.Property(e => e.Type).HasMaxLength(30);

            entity.HasOne(d => d.User).WithMany(p => p.OtpCodes)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("OtpCodes_UserId_fkey");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Payments_pkey");

            entity.HasIndex(e => e.OrderId, "Idx_Payments_Order");

            entity.HasIndex(e => e.Status, "Idx_Payments_Status");

            entity.HasIndex(e => e.OrderId, "Payments_OrderId_key").IsUnique();

            entity.Property(e => e.Amount).HasPrecision(15, 2);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.PaidAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.PaymentData).HasColumnType("jsonb");
            entity.Property(e => e.PaymentMethod).HasMaxLength(50);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValueSql("'PENDING'::character varying");
            entity.Property(e => e.TransactionId).HasMaxLength(100);

            entity.HasOne(d => d.Order).WithOne(p => p.Payment)
                .HasForeignKey<Payment>(d => d.OrderId)
                .HasConstraintName("Payments_OrderId_fkey");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Products_pkey");

            entity.HasIndex(e => new { e.IsActive, e.IsDeleted }, "Idx_Products_Active");

            entity.HasIndex(e => e.CategoryId, "Idx_Products_Category");

            entity.HasIndex(e => e.Gender, "Idx_Products_Gender");

            entity.HasIndex(e => e.Slug, "Idx_Products_Slug");

            entity.HasIndex(e => e.Slug, "Products_Slug_key").IsUnique();

            entity.Property(e => e.AverageRating)
                .HasPrecision(2, 1)
                .HasDefaultValueSql("0");
            entity.Property(e => e.BrandName).HasMaxLength(100);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Gender)
                .HasMaxLength(20)
                .HasDefaultValueSql("'UNISEX'::character varying");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.IsFeatured).HasDefaultValue(false);
            entity.Property(e => e.Material).HasMaxLength(255);
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.Price).HasPrecision(15, 2);
            entity.Property(e => e.SalePrice).HasPrecision(15, 2);
            entity.Property(e => e.Slug).HasMaxLength(255);
            entity.Property(e => e.SoldCount).HasDefaultValue(0);
            entity.Property(e => e.TotalReviews).HasDefaultValue(0);
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.ViewCount).HasDefaultValue(0);

            entity.HasOne(d => d.Category).WithMany(p => p.Products)
                .HasForeignKey(d => d.CategoryId)
                .HasConstraintName("Products_CategoryId_fkey");
        });

        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ProductImages_pkey");

            entity.HasIndex(e => e.ProductId, "Idx_ProductImages_Product");

            entity.Property(e => e.AltText).HasMaxLength(255);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.ImageUrl).HasMaxLength(500);
            entity.Property(e => e.IsPrimary).HasDefaultValue(false);
            entity.Property(e => e.SortOrder).HasDefaultValue(0);

            entity.HasOne(d => d.Product).WithMany(p => p.ProductImages)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("ProductImages_ProductId_fkey");
        });

        modelBuilder.Entity<ProductReview>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ProductReviews_pkey");

            entity.HasIndex(e => e.ProductId, "Idx_ProductReviews_Product");

            entity.HasIndex(e => new { e.ProductId, e.UserId }, "ProductReviews_ProductId_UserId_key").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.HeightCm).HasPrecision(5, 2);
            entity.Property(e => e.ReviewImageUrl).HasMaxLength(500);
            entity.Property(e => e.ShowBodyInfo).HasDefaultValue(false);
            entity.Property(e => e.SizeOrdered).HasMaxLength(20);
            entity.Property(e => e.WeightKg).HasPrecision(5, 2);

            entity.HasOne(d => d.Order).WithMany(p => p.ProductReviews)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("ProductReviews_OrderId_fkey");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductReviews)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("ProductReviews_ProductId_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.ProductReviews)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("ProductReviews_UserId_fkey");
        });

        modelBuilder.Entity<ProductVariant>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ProductVariants_pkey");

            entity.HasIndex(e => e.ProductId, "Idx_ProductVariants_Product");

            entity.HasIndex(e => new { e.ProductId, e.Size, e.Color }, "ProductVariants_ProductId_Size_Color_key").IsUnique();

            entity.HasIndex(e => e.Sku, "ProductVariants_Sku_key").IsUnique();

            entity.Property(e => e.Color).HasMaxLength(50);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PriceAdjustment)
                .HasPrecision(15, 2)
                .HasDefaultValueSql("0");
            entity.Property(e => e.Size).HasMaxLength(20);
            entity.Property(e => e.Sku).HasMaxLength(100);
            entity.Property(e => e.StockQuantity).HasDefaultValue(0);

            entity.HasOne(d => d.Product).WithMany(p => p.ProductVariants)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("ProductVariants_ProductId_fkey");
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("RefreshTokens_pkey");

            entity.HasIndex(e => new { e.UserId, e.IsRevoked }, "Idx_RefreshTokens_User");

            entity.HasIndex(e => e.Token, "RefreshTokens_Token_key").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.ExpiredAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.IsRevoked).HasDefaultValue(false);
            entity.Property(e => e.Token).HasMaxLength(255);

            entity.HasOne(d => d.User).WithMany(p => p.RefreshTokens)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("RefreshTokens_UserId_fkey");
        });

        modelBuilder.Entity<ShipperLocation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ShipperLocations_pkey");

            entity.HasIndex(e => new { e.OrderId, e.CreatedAt }, "Idx_ShipperLocations_Order").IsDescending(false, true);

            entity.HasIndex(e => new { e.ShipperId, e.CreatedAt }, "Idx_ShipperLocations_Shipper").IsDescending(false, true);

            entity.Property(e => e.Accuracy).HasPrecision(5, 1);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Heading).HasPrecision(5, 1);
            entity.Property(e => e.Latitude).HasPrecision(10, 7);
            entity.Property(e => e.Longitude).HasPrecision(10, 7);
            entity.Property(e => e.Speed).HasPrecision(5, 1);

            entity.HasOne(d => d.Order).WithMany(p => p.ShipperLocations)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("ShipperLocations_OrderId_fkey");

            entity.HasOne(d => d.Shipper).WithMany(p => p.ShipperLocations)
                .HasForeignKey(d => d.ShipperId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ShipperLocations_ShipperId_fkey");
        });

        modelBuilder.Entity<SizeGuide>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SizeGuides_pkey");

            entity.HasIndex(e => e.ProductId, "Idx_SizeGuides_Product");

            entity.HasIndex(e => new { e.ProductId, e.SizeName }, "SizeGuides_ProductId_SizeName_key").IsUnique();

            entity.Property(e => e.ChestCm).HasPrecision(5, 2);
            entity.Property(e => e.HipCm).HasPrecision(5, 2);
            entity.Property(e => e.LengthCm).HasPrecision(5, 2);
            entity.Property(e => e.MaxBust).HasPrecision(5, 2);
            entity.Property(e => e.MaxHips).HasPrecision(5, 2);
            entity.Property(e => e.MaxWaist).HasPrecision(5, 2);
            entity.Property(e => e.MaxWeight).HasPrecision(5, 2);
            entity.Property(e => e.MinBust).HasPrecision(5, 2);
            entity.Property(e => e.MinHips).HasPrecision(5, 2);
            entity.Property(e => e.MinWaist).HasPrecision(5, 2);
            entity.Property(e => e.MinWeight).HasPrecision(5, 2);
            entity.Property(e => e.ShoulderCm).HasPrecision(5, 2);
            entity.Property(e => e.SizeName).HasMaxLength(20);
            entity.Property(e => e.SleeveCm).HasPrecision(5, 2);
            entity.Property(e => e.WaistCm).HasPrecision(5, 2);

            entity.HasOne(d => d.Product).WithMany(p => p.SizeGuides)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("SizeGuides_ProductId_fkey");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Users_pkey");

            entity.HasIndex(e => e.Email, "Idx_Users_Email");

            entity.HasIndex(e => e.Phone, "Idx_Users_Phone");

            entity.HasIndex(e => e.Role, "Idx_Users_Role");

            entity.HasIndex(e => e.Email, "Users_Email_key").IsUnique();

            entity.HasIndex(e => e.Username, "Users_Username_key").IsUnique();

            entity.Property(e => e.AvatarUrl).HasMaxLength(500);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.Gender).HasMaxLength(10);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsEmailVerified).HasDefaultValue(false);
            entity.Property(e => e.Password).HasMaxLength(255);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.Role).HasDefaultValue(3);
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Username).HasMaxLength(50);
        });

        modelBuilder.Entity<UserAddress>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("UserAddresses_pkey");

            entity.HasIndex(e => e.UserId, "Idx_UserAddresses_UserId");

            entity.Property(e => e.AddressLine).HasMaxLength(255);
            entity.Property(e => e.City).HasMaxLength(100);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.District).HasMaxLength(100);
            entity.Property(e => e.IsDefault).HasDefaultValue(false);
            entity.Property(e => e.Latitude).HasPrecision(10, 7);
            entity.Property(e => e.Longitude).HasPrecision(10, 7);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.ReceiverName).HasMaxLength(100);
            entity.Property(e => e.Ward).HasMaxLength(100);

            entity.HasOne(d => d.User).WithMany(p => p.UserAddresses)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("UserAddresses_UserId_fkey");
        });

        modelBuilder.Entity<UserBodyProfile>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("UserBodyProfiles_pkey");

            entity.HasIndex(e => e.UserId, "UserBodyProfiles_UserId_key").IsUnique();

            entity.Property(e => e.Arm).HasPrecision(5, 2);
            entity.Property(e => e.BodyShape).HasMaxLength(50);
            entity.Property(e => e.Bust).HasPrecision(5, 2);
            entity.Property(e => e.FitPreference)
                .HasMaxLength(50)
                .HasDefaultValueSql("'Regular'::character varying");
            entity.Property(e => e.Height).HasPrecision(5, 2);
            entity.Property(e => e.Hips).HasPrecision(5, 2);
            entity.Property(e => e.Thigh).HasPrecision(5, 2);
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Waist).HasPrecision(5, 2);
            entity.Property(e => e.Weight).HasPrecision(5, 2);

            entity.HasOne(d => d.User).WithOne(p => p.UserBodyProfile)
                .HasForeignKey<UserBodyProfile>(d => d.UserId)
                .HasConstraintName("UserBodyProfiles_UserId_fkey");
        });

        modelBuilder.Entity<Voucher>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Vouchers_pkey");

            entity.HasIndex(e => new { e.IsActive, e.EndDate }, "Idx_Vouchers_Active");

            entity.HasIndex(e => e.Code, "Idx_Vouchers_Code");

            entity.HasIndex(e => e.Code, "Vouchers_Code_key").IsUnique();

            entity.Property(e => e.Code).HasMaxLength(30);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.DiscountType).HasMaxLength(20);
            entity.Property(e => e.DiscountValue).HasPrecision(15, 2);
            entity.Property(e => e.EndDate).HasColumnType("timestamp without time zone");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.MaxDiscountAmount).HasPrecision(15, 2);
            entity.Property(e => e.MinOrderAmount)
                .HasPrecision(15, 2)
                .HasDefaultValueSql("0");
            entity.Property(e => e.StartDate).HasColumnType("timestamp without time zone");
            entity.Property(e => e.UsageLimit).HasDefaultValue(100);
            entity.Property(e => e.UsedCount).HasDefaultValue(0);
        });

        modelBuilder.Entity<Wishlist>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Wishlists_pkey");

            entity.HasIndex(e => e.UserId, "Idx_Wishlists_User");

            entity.HasIndex(e => new { e.UserId, e.ProductId }, "Wishlists_UserId_ProductId_key").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Product).WithMany(p => p.Wishlists)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("Wishlists_ProductId_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Wishlists)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("Wishlists_UserId_fkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
