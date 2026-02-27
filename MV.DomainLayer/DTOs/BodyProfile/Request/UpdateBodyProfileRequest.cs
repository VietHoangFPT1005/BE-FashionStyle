using System.ComponentModel.DataAnnotations;

namespace MV.DomainLayer.DTOs.BodyProfile.Request
{
    public class UpdateBodyProfileRequest
    {
        [Range(100, 250, ErrorMessage = "Height must be between 100 and 250 cm.")]
        public decimal? Height { get; set; }

        [Range(30, 300, ErrorMessage = "Weight must be between 30 and 300 kg.")]
        public decimal? Weight { get; set; }

        [Range(60, 200, ErrorMessage = "Bust must be between 60 and 200 cm.")]
        public decimal? Bust { get; set; }

        [Range(50, 200, ErrorMessage = "Waist must be between 50 and 200 cm.")]
        public decimal? Waist { get; set; }

        [Range(60, 200, ErrorMessage = "Hips must be between 60 and 200 cm.")]
        public decimal? Hips { get; set; }

        [Range(20, 80, ErrorMessage = "Arm must be between 20 and 80 cm.")]
        public decimal? Arm { get; set; }

        [Range(30, 100, ErrorMessage = "Thigh must be between 30 and 100 cm.")]
        public decimal? Thigh { get; set; }

        [RegularExpression(@"^(PEAR|APPLE|HOURGLASS|RECTANGLE|INVERTED_TRIANGLE)$",
            ErrorMessage = "Body shape must be PEAR, APPLE, HOURGLASS, RECTANGLE, or INVERTED_TRIANGLE.")]
        public string? BodyShape { get; set; }

        [RegularExpression(@"^(Tight|Regular|Loose)$",
            ErrorMessage = "Fit preference must be Tight, Regular, or Loose.")]
        public string? FitPreference { get; set; }
    }
}
