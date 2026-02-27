using System.ComponentModel.DataAnnotations;

namespace MV.DomainLayer.DTOs.Order.Request
{
    public class CreateOrderRequest
    {
        [Required(ErrorMessage = "Address ID is required.")]
        public int AddressId { get; set; }

        [Required(ErrorMessage = "Payment method is required.")]
        [RegularExpression("^(COD|SEPAY)$", ErrorMessage = "Payment method must be COD or SEPAY.")]
        public string PaymentMethod { get; set; } = null!;

        public string? VoucherCode { get; set; }

        [MaxLength(500, ErrorMessage = "Note must not exceed 500 characters.")]
        public string? Note { get; set; }
    }
}
