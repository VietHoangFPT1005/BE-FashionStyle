using System.ComponentModel.DataAnnotations;

namespace MV.DomainLayer.DTOs.Admin.Request
{
    public class CreateVoucherRequest
    {
        [Required, MaxLength(30)]
        public string Code { get; set; } = null!;

        [Required, MaxLength(255)]
        public string Description { get; set; } = null!;

        /// <summary>
        /// PERCENTAGE or FIXED_AMOUNT
        /// </summary>
        [Required, MaxLength(20)]
        public string DiscountType { get; set; } = null!;

        [Required, Range(0.01, 999999999)]
        public decimal DiscountValue { get; set; }

        [Range(0, 999999999)]
        public decimal? MinOrderAmount { get; set; }

        [Range(0, 999999999)]
        public decimal? MaxDiscountAmount { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Range(1, 999999)]
        public int? UsageLimit { get; set; }
    }

    public class UpdateVoucherRequest
    {
        [MaxLength(255)]
        public string? Description { get; set; }

        [MaxLength(20)]
        public string? DiscountType { get; set; }

        [Range(0.01, 999999999)]
        public decimal? DiscountValue { get; set; }

        [Range(0, 999999999)]
        public decimal? MinOrderAmount { get; set; }

        [Range(0, 999999999)]
        public decimal? MaxDiscountAmount { get; set; }

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        [Range(1, 999999)]
        public int? UsageLimit { get; set; }

        public bool? IsActive { get; set; }
    }
}
