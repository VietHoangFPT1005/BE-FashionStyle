using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.DTOs.Notification.Request;
using MV.DomainLayer.DTOs.Notification.Response;

namespace MV.ApplicationLayer.ServiceInterfaces
{
    public interface INotificationService
    {
        Task<ApiResponse<NotificationListResponse>> GetNotificationsAsync(int userId, int page, int pageSize);
        Task<ApiResponse<object>> MarkAsReadAsync(int userId, int notificationId);
        Task<ApiResponse<object>> MarkAllAsReadAsync(int userId);
        Task<ApiResponse<object>> BroadcastAsync(BroadcastNotificationRequest request);
    }
}
