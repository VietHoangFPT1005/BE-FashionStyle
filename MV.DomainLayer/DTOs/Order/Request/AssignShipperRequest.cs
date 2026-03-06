using System.ComponentModel.DataAnnotations;

namespace MV.DomainLayer.DTOs.Order.Request
{
    public class AssignShipperRequest
    {
        [Required(ErrorMessage = "Shipper ID is required.")]
        public int ShipperId { get; set; }
    }
}
