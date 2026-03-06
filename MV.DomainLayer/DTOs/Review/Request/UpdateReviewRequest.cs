using System.ComponentModel.DataAnnotations;

namespace MV.DomainLayer.DTOs.Review.Request
{
    public class UpdateReviewRequest
    {
        [Range(1, 5)]
        public int? Rating { get; set; }

        public string? Comment { get; set; }

        [MaxLength(500)]
        public string? ReviewImageUrl { get; set; }

        public bool? ShowBodyInfo { get; set; }
    }
}
