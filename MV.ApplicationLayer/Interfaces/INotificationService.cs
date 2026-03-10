using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.DTOs.Notification.Request;
using MV.DomainLayer.DTOs.Notification.Response;

namespace MV.ApplicationLayer.ServiceInterfaces
{
    public interface INotificationService
    {
        // ==================== CRUD APIs ====================
        Task<ApiResponse<NotificationListResponse>> GetNotificationsAsync(int userId, int page, int pageSize);
        Task<ApiResponse<object>> MarkAsReadAsync(int userId, int notificationId);
        Task<ApiResponse<object>> MarkAllAsReadAsync(int userId);
        Task<ApiResponse<object>> BroadcastAsync(BroadcastNotificationRequest request);
        Task<ApiResponse<object>> GetUnreadCountAsync(int userId);
        Task<ApiResponse<object>> DeleteNotificationAsync(int userId, int notificationId);

        // ==================== Business Event Senders ====================
        /// <summary>Send notification when order status changes (confirmed, processing, shipping, delivered, cancelled)</summary>
        Task SendOrderStatusChangedAsync(int userId, int orderId, string orderCode, string newStatus, string? reason = null);

        /// <summary>Send notification when payment is confirmed (webhook or admin verify)</summary>
        Task SendPaymentConfirmedAsync(int userId, int orderId, string orderCode, decimal amount);

        /// <summary>Send notification when payment expires (background service)</summary>
        Task SendPaymentExpiredAsync(int userId, int orderId, string orderCode);

        /// <summary>Send notification for shipping updates (pickup, delivery failed, delivered)</summary>
        Task SendShippingUpdateAsync(int userId, int orderId, string orderCode, string updateType, string? detail = null);
    }
}
