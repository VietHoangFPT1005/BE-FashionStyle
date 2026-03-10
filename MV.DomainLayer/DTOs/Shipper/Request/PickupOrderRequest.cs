using System.ComponentModel.DataAnnotations;

namespace MV.DomainLayer.DTOs.Shipper.Request
{
    public class PickupOrderRequest
    {
        /// <summary>
        /// Mã vận đơn / tracking number (bắt buộc khi bắt đầu giao hàng)
        /// </summary>
        [Required(ErrorMessage = "Tracking number is required.")]
        [StringLength(100, ErrorMessage = "Tracking number must not exceed 100 characters.")]
        public string TrackingNumber { get; set; } = null!;

        /// <summary>
        /// Đơn vị vận chuyển (optional, ví dụ: GHN, GHTK, J&T, Shipper nội bộ)
        /// </summary>
        [StringLength(100, ErrorMessage = "Carrier must not exceed 100 characters.")]
        public string? Carrier { get; set; }

        /// <summary>
        /// Ngày dự kiến giao hàng (optional)
        /// </summary>
        public DateTime? ExpectedDeliveryDate { get; set; }
    }
}
