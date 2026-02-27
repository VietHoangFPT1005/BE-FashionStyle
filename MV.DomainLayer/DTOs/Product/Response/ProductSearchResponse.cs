namespace MV.DomainLayer.DTOs.Product.Response
{
    public class ProductSearchResponse
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public decimal Price { get; set; }
        public decimal? SalePrice { get; set; }
        public string? PrimaryImage { get; set; }
    }
}
