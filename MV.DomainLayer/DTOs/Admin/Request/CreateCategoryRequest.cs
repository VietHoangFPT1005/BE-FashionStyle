using System.ComponentModel.DataAnnotations;

namespace MV.DomainLayer.DTOs.Admin.Request
{
    public class CreateCategoryRequest
    {
        [Required, MaxLength(100)]
        public string Name { get; set; } = null!;

        [Required, MaxLength(100)]
        public string Slug { get; set; } = null!;

        public string? Description { get; set; }

        [MaxLength(500)]
        public string? ImageUrl { get; set; }

        public int? ParentId { get; set; }

        public int? SortOrder { get; set; }
    }

    public class UpdateCategoryRequest
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        [MaxLength(100)]
        public string? Slug { get; set; }

        public string? Description { get; set; }

        [MaxLength(500)]
        public string? ImageUrl { get; set; }

        public int? ParentId { get; set; }

        public int? SortOrder { get; set; }

        public bool? IsActive { get; set; }
    }
}
