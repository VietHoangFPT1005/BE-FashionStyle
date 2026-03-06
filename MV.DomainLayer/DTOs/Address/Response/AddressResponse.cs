namespace MV.DomainLayer.DTOs.Address.Response
{
    public class AddressResponse
    {
        public int AddressId { get; set; }
        public string ReceiverName { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public string AddressLine { get; set; } = null!;
        public string? Ward { get; set; }
        public string District { get; set; } = null!;
        public string City { get; set; } = null!;
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public bool IsDefault { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
