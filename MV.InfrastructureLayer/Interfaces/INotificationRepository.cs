using MV.DomainLayer.Entities;

namespace MV.InfrastructureLayer.Interfaces
{
    public interface INotificationRepository
    {
        Task<Notification> CreateAsync(Notification notification);
        Task CreateRangeAsync(List<Notification> notifications);
        Task<(List<Notification> Items, int TotalCount)> GetByUserIdPagedAsync(int userId, int page, int pageSize);
        Task<int> GetUnreadCountAsync(int userId);
        Task<Notification?> GetByIdAndUserIdAsync(int notificationId, int userId);
        Task MarkAsReadAsync(int notificationId);
        Task MarkAllAsReadAsync(int userId);
    }
}
