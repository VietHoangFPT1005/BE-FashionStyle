namespace MV.DomainLayer.DTOs.Voucher.Response
{
    public class VoucherResponse
    {
        public int VoucherId { get; set; }
        public string Code { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string DiscountType { get; set; } = null!;
        public decimal DiscountValue { get; set; }
        public decimal? MinOrderAmount { get; set; }
        public decimal? MaxDiscountAmount { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int? UsageLimit { get; set; }
        public int? UsedCount { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
