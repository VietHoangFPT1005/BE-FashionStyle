namespace MV.DomainLayer.DTOs.Product.Response
{
    public class ProductListResponse
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public decimal Price { get; set; }
        public decimal? SalePrice { get; set; }
        public string? Gender { get; set; }
        public string? BrandName { get; set; }
        public List<string>? Tags { get; set; }
        public string? PrimaryImage { get; set; }
        public ProductCategoryInfo? Category { get; set; }
        public decimal? AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public int SoldCount { get; set; }
        public bool InStock { get; set; }
        public bool IsFeatured { get; set; }
        public List<string> AvailableSizes { get; set; } = new();
    }

    public class ProductCategoryInfo
    {
        public int CategoryId { get; set; }
        public string Name { get; set; } = null!;
    }
}
