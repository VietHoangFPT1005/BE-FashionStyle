namespace MV.DomainLayer.DTOs.Product.Response
{
    public class RecommendSizeResponse
    {
        public string RecommendedSize { get; set; } = null!;
        public int FitScore { get; set; }
        public string FitLevel { get; set; } = null!;
        public UserBodyInfo UserProfile { get; set; } = new();
        public List<SizeComparisonItem> SizeComparison { get; set; } = new();
        public string Suggestion { get; set; } = null!;
    }

    public class UserBodyInfo
    {
        public decimal? Bust { get; set; }
        public decimal? Waist { get; set; }
        public decimal? Hips { get; set; }
        public decimal? Weight { get; set; }
    }

    public class SizeComparisonItem
    {
        public string SizeName { get; set; } = null!;
        public int FitScore { get; set; }
        public string FitLevel { get; set; } = null!;
        public string Details { get; set; } = null!;
        public bool InStock { get; set; }
    }
}
