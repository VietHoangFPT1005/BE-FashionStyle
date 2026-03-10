namespace MV.DomainLayer.DTOs.Payment.Response
{
    public class SePayPaymentResponse
    {
        public string QrCodeUrl { get; set; } = null!;
        public decimal Amount { get; set; }
        public string OrderCode { get; set; } = null!;
        public string AccountNumber { get; set; } = null!;
        public string AccountName { get; set; } = null!;
        public string BankName { get; set; } = null!;
        public string Description { get; set; } = null!;
        public DateTime ExpiredAt { get; set; }
    }

    public class PaymentStatusResponse
    {
        public int OrderId { get; set; }
        public string OrderCode { get; set; } = null!;
        public string PaymentMethod { get; set; } = null!;
        public decimal Amount { get; set; }
        public string? Status { get; set; }
        public string? TransactionId { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? ExpiredAt { get; set; }
        public bool IsPaid { get; set; }
        public int RemainingSeconds { get; set; }
    }
}
