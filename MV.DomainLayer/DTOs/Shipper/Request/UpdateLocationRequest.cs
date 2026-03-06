using System.ComponentModel.DataAnnotations;

namespace MV.DomainLayer.DTOs.Shipper.Request
{
    public class UpdateLocationRequest
    {
        [Required(ErrorMessage = "Order ID is required.")]
        public int OrderId { get; set; }

        [Required(ErrorMessage = "Latitude is required.")]
        [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90.")]
        public decimal Latitude { get; set; }

        [Required(ErrorMessage = "Longitude is required.")]
        [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180.")]
        public decimal Longitude { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Accuracy must be >= 0.")]
        public decimal? Accuracy { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Speed must be >= 0.")]
        public decimal? Speed { get; set; }

        [Range(0, 360, ErrorMessage = "Heading must be between 0 and 360.")]
        public decimal? Heading { get; set; }
    }
}
