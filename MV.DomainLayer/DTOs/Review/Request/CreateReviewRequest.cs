using System.ComponentModel.DataAnnotations;

namespace MV.DomainLayer.DTOs.Review.Request
{
    public class CreateReviewRequest
    {
        [Required(ErrorMessage = "Rating is required.")]
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
        public int Rating { get; set; }

        [MaxLength(1000, ErrorMessage = "Comment must not exceed 1000 characters.")]
        public string? Comment { get; set; }

        public string? ReviewImageUrl { get; set; }

        [Range(100, 250, ErrorMessage = "Height must be between 100 and 250 cm.")]
        public decimal? HeightCm { get; set; }

        [Range(30, 300, ErrorMessage = "Weight must be between 30 and 300 kg.")]
        public decimal? WeightKg { get; set; }

        public string? SizeOrdered { get; set; }

        public bool ShowBodyInfo { get; set; } = false;
    }
}
