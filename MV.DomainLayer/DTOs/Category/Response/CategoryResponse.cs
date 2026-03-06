namespace MV.DomainLayer.DTOs.Category.Response
{
    public class CategoryResponse
    {
        public int CategoryId { get; set; }
        public string Name { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public string? ImageUrl { get; set; }
        public int? ParentId { get; set; }
        public int ProductCount { get; set; }
        public List<CategoryResponse>? Children { get; set; }
    }
}
