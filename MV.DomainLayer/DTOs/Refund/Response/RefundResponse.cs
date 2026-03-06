namespace MV.DomainLayer.DTOs.Refund.Response
{
    public class RefundResponse
    {
        public int RefundId { get; set; }
        public int OrderId { get; set; }
        public string OrderCode { get; set; } = null!;
        public int UserId { get; set; }
        public string? CustomerName { get; set; }
        public string Reason { get; set; } = null!;
        public string? AdminNote { get; set; }
        public string Status { get; set; } = null!;
        public decimal OrderTotal { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public int? ProcessedBy { get; set; }
        public string? ProcessedByName { get; set; }
    }
}
