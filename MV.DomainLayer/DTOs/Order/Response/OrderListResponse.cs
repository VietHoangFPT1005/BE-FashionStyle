namespace MV.DomainLayer.DTOs.Order.Response
{
    public class OrderListResponse
    {
        public int OrderId { get; set; }
        public string OrderCode { get; set; } = null!;
        public string Status { get; set; } = null!;
        public decimal Total { get; set; }
        public int TotalItems { get; set; }
        public string PaymentMethod { get; set; } = null!;
        public string? PaymentStatus { get; set; }
        public string? FirstItemImage { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
