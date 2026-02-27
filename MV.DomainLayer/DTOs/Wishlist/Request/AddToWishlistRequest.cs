using System.ComponentModel.DataAnnotations;

namespace MV.DomainLayer.DTOs.Wishlist.Request
{
    public class AddToWishlistRequest
    {
        [Required(ErrorMessage = "Product ID is required.")]
        public int ProductId { get; set; }
    }
}
