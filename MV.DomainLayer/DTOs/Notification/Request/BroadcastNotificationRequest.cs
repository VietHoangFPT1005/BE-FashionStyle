using System.ComponentModel.DataAnnotations;

namespace MV.DomainLayer.DTOs.Notification.Request
{
    public class BroadcastNotificationRequest
    {
        [Required(ErrorMessage = "Notification type is required.")]
        [StringLength(50, ErrorMessage = "Type must not exceed 50 characters.")]
        public string Type { get; set; } = string.Empty;

        [Required(ErrorMessage = "Title is required.")]
        [StringLength(200, ErrorMessage = "Title must not exceed 200 characters.")]
        public string Title { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "Message must not exceed 1000 characters.")]
        public string? Message { get; set; }

        public string? Data { get; set; }
    }
}
