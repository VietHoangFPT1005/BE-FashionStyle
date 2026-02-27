using System.ComponentModel.DataAnnotations;

namespace MV.DomainLayer.DTOs.Cart.Request
{
    public class UpdateCartItemRequest
    {
        [Required(ErrorMessage = "Quantity is required.")]
        [Range(0, 100, ErrorMessage = "Quantity must be between 0 and 100.")]
        public int Quantity { get; set; }
    }
}
