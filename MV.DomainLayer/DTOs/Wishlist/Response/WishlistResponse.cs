namespace MV.DomainLayer.DTOs.Wishlist.Response
{
    public class WishlistItemResponse
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = null!;
        public decimal Price { get; set; }
        public decimal? SalePrice { get; set; }
        public string? PrimaryImage { get; set; }
        public bool InStock { get; set; }
        public DateTime? AddedAt { get; set; }
    }
}
