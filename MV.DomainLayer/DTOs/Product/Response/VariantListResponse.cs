namespace MV.DomainLayer.DTOs.Product.Response
{
    public class VariantListResponse
    {
        public List<string> Colors { get; set; } = new();
        public List<string> Sizes { get; set; } = new();
        public List<ProductVariantResponse> Variants { get; set; } = new();
    }
}
