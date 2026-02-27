using System.ComponentModel.DataAnnotations;

namespace MV.DomainLayer.DTOs.Payment.Request
{
    public class CreatePaymentRequest
    {
        [Required(ErrorMessage = "Order ID is required.")]
        public int OrderId { get; set; }
    }
}
