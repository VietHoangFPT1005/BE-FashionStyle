namespace MV.DomainLayer.DTOs.Shipper.Response
{
    public class TrackingResponse
    {
        public int OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public TrackingShipperInfo Shipper { get; set; } = new();
        public TrackingCurrentLocation? CurrentLocation { get; set; }
        public TrackingDestination Destination { get; set; } = new();
        public TrackingTimeline Timeline { get; set; } = new();
    }

    public class TrackingShipperInfo
    {
        public int ShipperId { get; set; }
        public string? FullName { get; set; }
        public string? Phone { get; set; }
    }

    public class TrackingCurrentLocation
    {
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public decimal? Accuracy { get; set; }
        public decimal? Speed { get; set; }
        public decimal? Heading { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class TrackingDestination
    {
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string Address { get; set; } = string.Empty;
    }

    public class TrackingTimeline
    {
        public DateTime? ShippedAt { get; set; }
        public int? EstimatedMinutes { get; set; }
    }
}
