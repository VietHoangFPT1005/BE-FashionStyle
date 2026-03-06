using System.ComponentModel.DataAnnotations;

namespace MV.DomainLayer.DTOs.Refund.Request
{
    public class CreateRefundRequest
    {
        [Required, MinLength(10), MaxLength(1000)]
        public string Reason { get; set; } = null!;
    }

    public class ProcessRefundRequest
    {
        [MaxLength(500)]
        public string? AdminNote { get; set; }
    }
}
