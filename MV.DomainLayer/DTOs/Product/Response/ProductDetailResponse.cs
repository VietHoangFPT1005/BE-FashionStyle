namespace MV.DomainLayer.DTOs.Product.Response
{
    public class ProductDetailResponse
    {
        public int ProductId { get; set; }
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
        public ProductCategoryInfo? Category { get; set; }
        public List<ProductImageResponse> Images { get; set; } = new();
        public List<ProductVariantResponse> Variants { get; set; } = new();
        public bool HasSizeGuide { get; set; }
        public decimal? AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public int ViewCount { get; set; }
        public int SoldCount { get; set; }
        public bool InStock { get; set; }
        public bool IsFeatured { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class ProductImageResponse
    {
        public int ImageId { get; set; }
        public string ImageUrl { get; set; } = null!;
        public string? AltText { get; set; }
        public bool IsPrimary { get; set; }
        public int SortOrder { get; set; }
    }

    public class ProductVariantResponse
    {
        public int VariantId { get; set; }
        public string Sku { get; set; } = null!;
        public string Size { get; set; } = null!;
        public string Color { get; set; } = null!;
        public int StockQuantity { get; set; }
        public decimal PriceAdjustment { get; set; }
        public bool InStock { get; set; }
    }
}
