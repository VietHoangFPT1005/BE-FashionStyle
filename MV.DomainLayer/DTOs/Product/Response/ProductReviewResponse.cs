namespace MV.DomainLayer.DTOs.Product.Response
{
    public class ProductReviewListResponse
    {
        public ReviewSummary Summary { get; set; } = new();
        public List<ReviewItemResponse> Reviews { get; set; } = new();
        public Common.PaginationInfo Pagination { get; set; } = new();
    }

    public class ReviewSummary
    {
        public decimal AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public Dictionary<int, int> RatingDistribution { get; set; } = new();
    }

    public class ReviewItemResponse
    {
        public int ReviewId { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public string? ReviewImageUrl { get; set; }
        public string? SizeOrdered { get; set; }
        public ReviewBodyInfo? BodyInfo { get; set; }
        public ReviewUserInfo User { get; set; } = new();
        public DateTime? CreatedAt { get; set; }
    }

    public class ReviewBodyInfo
    {
        public decimal? HeightCm { get; set; }
        public decimal? WeightKg { get; set; }
        public bool ShowBodyInfo { get; set; }
    }

    public class ReviewUserInfo
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = null!;
        public string? AvatarUrl { get; set; }
    }
}
