using System.ComponentModel.DataAnnotations;

namespace MV.DomainLayer.DTOs.Voucher.Request
{
    public class ValidateVoucherRequest
    {
        [Required(ErrorMessage = "Voucher code is required.")]
        public string VoucherCode { get; set; } = null!;
    }
}
