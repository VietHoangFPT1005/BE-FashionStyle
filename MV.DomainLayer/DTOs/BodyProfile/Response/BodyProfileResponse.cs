namespace MV.DomainLayer.DTOs.BodyProfile.Response
{
    public class BodyProfileResponse
    {
        public decimal? Height { get; set; }
        public decimal? Weight { get; set; }
        public decimal? Bust { get; set; }
        public decimal? Waist { get; set; }
        public decimal? Hips { get; set; }
        public decimal? Arm { get; set; }
        public decimal? Thigh { get; set; }
        public string? BodyShape { get; set; }
        public string? FitPreference { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
