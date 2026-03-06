namespace MV.DomainLayer.DTOs.Review.Response
{
    public class CreateReviewResponse
    {
        public int ReviewId { get; set; }
        public int ProductId { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public ReviewBodyInfoResponse? BodyInfo { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class ReviewBodyInfoResponse
    {
        public decimal? HeightCm { get; set; }
        public decimal? WeightKg { get; set; }
        public string? SizeOrdered { get; set; }
    }
}
