using System.ComponentModel.DataAnnotations;

namespace MV.DomainLayer.DTOs.Order.Request
{
    public class CancelOrderRequest
    {
        [MaxLength(500, ErrorMessage = "Cancel reason must not exceed 500 characters.")]
        public string? CancelReason { get; set; }
    }
}
