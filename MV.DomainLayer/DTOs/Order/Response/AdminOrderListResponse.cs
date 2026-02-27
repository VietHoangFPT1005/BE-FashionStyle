namespace MV.DomainLayer.DTOs.Order.Response
{
    public class AdminOrderListResponse
    {
        public int OrderId { get; set; }
        public string OrderCode { get; set; } = null!;
        public OrderCustomerInfo Customer { get; set; } = new();
        public OrderShipperInfo? Shipper { get; set; }
        public string Status { get; set; } = null!;
        public decimal Total { get; set; }
        public string PaymentMethod { get; set; } = null!;
        public string? PaymentStatus { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
