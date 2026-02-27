using System;
using System.Collections.Generic;

namespace MV.DomainLayer.Entities;

public partial class Product
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string? Description { get; set; }

    public string? DetailDescription { get; set; }

    public string? Material { get; set; }

    public string? CareInstructions { get; set; }

    public string? Gender { get; set; }

    public string? BrandName { get; set; }

    public List<string>? Tags { get; set; }

    public decimal Price { get; set; }

    public decimal? SalePrice { get; set; }

    public int? CategoryId { get; set; }

    public decimal? AverageRating { get; set; }

    public int? TotalReviews { get; set; }

    public int? ViewCount { get; set; }

    public int? SoldCount { get; set; }

    public bool? IsActive { get; set; }

    public bool? IsFeatured { get; set; }

    public bool? IsDeleted { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Category? Category { get; set; }

    public virtual ICollection<ProductImage> ProductImages { get; set; } = new List<ProductImage>();

    public virtual ICollection<ProductReview> ProductReviews { get; set; } = new List<ProductReview>();

    public virtual ICollection<ProductVariant> ProductVariants { get; set; } = new List<ProductVariant>();

    public virtual ICollection<SizeGuide> SizeGuides { get; set; } = new List<SizeGuide>();

    public virtual ICollection<Wishlist> Wishlists { get; set; } = new List<Wishlist>();
}
