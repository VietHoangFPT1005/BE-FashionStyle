namespace MV.DomainLayer.DTOs.Admin.Response
{
    public class AdminProductResponse
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public string? Description { get; set; }
        public string? Material { get; set; }
        public string? Gender { get; set; }
        public string? BrandName { get; set; }
        public List<string>? Tags { get; set; }
        public decimal Price { get; set; }
        public decimal? SalePrice { get; set; }
        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public decimal? AverageRating { get; set; }
        public int? TotalReviews { get; set; }
        public int? ViewCount { get; set; }
        public int? SoldCount { get; set; }
        public bool? IsActive { get; set; }
        public bool? IsFeatured { get; set; }
        public int VariantCount { get; set; }
        public int TotalStock { get; set; }
        public string? PrimaryImageUrl { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class AdminProductDetailResponse : AdminProductResponse
    {
        public string? DetailDescription { get; set; }
        public string? CareInstructions { get; set; }
        public List<AdminVariantResponse> Variants { get; set; } = new();
        public List<AdminProductImageResponse> Images { get; set; } = new();
        public List<AdminSizeGuideResponse> SizeGuides { get; set; } = new();
    }

    public class AdminVariantResponse
    {
        public int VariantId { get; set; }
        public string Sku { get; set; } = null!;
        public string Size { get; set; } = null!;
        public string Color { get; set; } = null!;
        public int? StockQuantity { get; set; }
        public decimal? PriceAdjustment { get; set; }
        public bool? IsActive { get; set; }
    }

    public class AdminProductImageResponse
    {
        public int ImageId { get; set; }
        public string ImageUrl { get; set; } = null!;
        public string? AltText { get; set; }
        public bool? IsPrimary { get; set; }
        public int? SortOrder { get; set; }
    }

    public class AdminSizeGuideResponse
    {
        public int SizeGuideId { get; set; }
        public string SizeName { get; set; } = null!;
        public decimal? MinBust { get; set; }
        public decimal? MaxBust { get; set; }
        public decimal? MinWaist { get; set; }
        public decimal? MaxWaist { get; set; }
        public decimal? MinHips { get; set; }
        public decimal? MaxHips { get; set; }
        public decimal? MinWeight { get; set; }
        public decimal? MaxWeight { get; set; }
        public decimal? ChestCm { get; set; }
        public decimal? WaistCm { get; set; }
        public decimal? HipCm { get; set; }
        public decimal? ShoulderCm { get; set; }
        public decimal? LengthCm { get; set; }
        public decimal? SleeveCm { get; set; }
    }
}
