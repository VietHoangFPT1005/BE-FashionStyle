using System.ComponentModel.DataAnnotations;

namespace MV.DomainLayer.DTOs.Admin.Request
{
    public class CreateVariantRequest
    {
        [Required, MaxLength(100)]
        public string Sku { get; set; } = null!;

        [Required, MaxLength(20)]
        public string Size { get; set; } = null!;

        [Required, MaxLength(50)]
        public string Color { get; set; } = null!;

        [Range(0, 999999)]
        public int? StockQuantity { get; set; }

        [Range(-999999, 999999)]
        public decimal? PriceAdjustment { get; set; }
    }

    public class UpdateVariantRequest
    {
        [MaxLength(100)]
        public string? Sku { get; set; }

        [MaxLength(20)]
        public string? Size { get; set; }

        [MaxLength(50)]
        public string? Color { get; set; }

        [Range(0, 999999)]
        public int? StockQuantity { get; set; }

        [Range(-999999, 999999)]
        public decimal? PriceAdjustment { get; set; }

        public bool? IsActive { get; set; }
    }
}
