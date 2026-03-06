namespace MV.DomainLayer.DTOs.Voucher.Response
{
    public class VoucherValidationResponse
    {
        public int VoucherId { get; set; }
        public string Code { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string DiscountType { get; set; } = null!;
        public decimal DiscountValue { get; set; }
        public decimal CalculatedDiscount { get; set; }
        public decimal? MaxDiscountAmount { get; set; }
        public decimal CartSubtotal { get; set; }
        public decimal NewTotal { get; set; }
        public string Message { get; set; } = null!;
    }
}
