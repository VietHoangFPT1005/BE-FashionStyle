using System.ComponentModel.DataAnnotations;

namespace MV.DomainLayer.DTOs.Shipper.Request
{
    public class DeliveryFailedRequest
    {
        [StringLength(500, ErrorMessage = "Reason must not exceed 500 characters.")]
        public string? Reason { get; set; }
    }
}
