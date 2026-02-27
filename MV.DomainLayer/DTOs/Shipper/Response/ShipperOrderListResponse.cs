namespace MV.DomainLayer.DTOs.Shipper.Response
{
    public class ShipperOrderListResponse
    {
        public int OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public int TotalItems { get; set; }
        public ShipperShippingInfo ShippingInfo { get; set; } = new();
        public DateTime? CreatedAt { get; set; }
    }

    public class ShipperShippingInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
    }
}
