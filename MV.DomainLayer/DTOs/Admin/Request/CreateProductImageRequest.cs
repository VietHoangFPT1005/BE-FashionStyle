using System.ComponentModel.DataAnnotations;

namespace MV.DomainLayer.DTOs.Admin.Request
{
    public class CreateProductImageRequest
    {
        [Required, MaxLength(500)]
        public string ImageUrl { get; set; } = null!;

        [MaxLength(255)]
        public string? AltText { get; set; }

        public bool? IsPrimary { get; set; }

        public int? SortOrder { get; set; }
    }
}
