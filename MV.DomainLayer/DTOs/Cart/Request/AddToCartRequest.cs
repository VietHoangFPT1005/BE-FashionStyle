using System.ComponentModel.DataAnnotations;

namespace MV.DomainLayer.DTOs.Cart.Request
{
    public class AddToCartRequest
    {
        [Required(ErrorMessage = "Product variant ID is required.")]
        public int ProductVariantId { get; set; }

        [Required(ErrorMessage = "Quantity is required.")]
        [Range(1, 100, ErrorMessage = "Quantity must be between 1 and 100.")]
        public int Quantity { get; set; }
    }
}
