using System.ComponentModel.DataAnnotations;

namespace MV.DomainLayer.DTOs.Admin.Request
{
    public class CreateProductRequest
    {
        [Required, MaxLength(255)]
        public string Name { get; set; } = null!;

        [Required, MaxLength(255)]
        public string Slug { get; set; } = null!;

        public string? Description { get; set; }

        public string? DetailDescription { get; set; }

        [MaxLength(255)]
        public string? Material { get; set; }

        public string? CareInstructions { get; set; }

        [MaxLength(20)]
        public string? Gender { get; set; }

        [MaxLength(100)]
        public string? BrandName { get; set; }

        public List<string>? Tags { get; set; }

        [Required, Range(0, 999999999)]
        public decimal Price { get; set; }

        [Range(0, 999999999)]
        public decimal? SalePrice { get; set; }

        public int? CategoryId { get; set; }

        public bool? IsFeatured { get; set; }
    }

    public class UpdateProductRequest
    {
        [MaxLength(255)]
        public string? Name { get; set; }

        [MaxLength(255)]
        public string? Slug { get; set; }

        public string? Description { get; set; }

        public string? DetailDescription { get; set; }

        [MaxLength(255)]
        public string? Material { get; set; }

        public string? CareInstructions { get; set; }

        [MaxLength(20)]
        public string? Gender { get; set; }

        [MaxLength(100)]
        public string? BrandName { get; set; }

        public List<string>? Tags { get; set; }

        [Range(0, 999999999)]
        public decimal? Price { get; set; }

        [Range(0, 999999999)]
        public decimal? SalePrice { get; set; }

        public int? CategoryId { get; set; }

        public bool? IsActive { get; set; }

        public bool? IsFeatured { get; set; }
    }
}
