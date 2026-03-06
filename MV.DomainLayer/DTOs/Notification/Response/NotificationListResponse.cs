using MV.DomainLayer.DTOs.Common;

namespace MV.DomainLayer.DTOs.Notification.Response
{
    public class NotificationListResponse
    {
        public int UnreadCount { get; set; }
        public List<NotificationItemResponse> Items { get; set; } = new();
        public PaginationInfo Pagination { get; set; } = new();
    }

    public class NotificationItemResponse
    {
        public int NotificationId { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Message { get; set; }
        public string? Data { get; set; }
        public bool IsRead { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
