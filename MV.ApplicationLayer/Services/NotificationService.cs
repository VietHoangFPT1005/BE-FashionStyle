using Microsoft.EntityFrameworkCore;
using MV.ApplicationLayer.ServiceInterfaces;
using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.DTOs.Notification.Request;
using MV.DomainLayer.DTOs.Notification.Response;
using MV.DomainLayer.Entities;
using MV.InfrastructureLayer.DBContext;
using MV.InfrastructureLayer.Interfaces;

namespace MV.ApplicationLayer.Services
{
    public class NotificationService : INotificationService
    {
        private readonly FashionDbContext _context;
        private readonly INotificationRepository _notificationRepository;

        public NotificationService(
            FashionDbContext context,
            INotificationRepository notificationRepository)
        {
            _context = context;
            _notificationRepository = notificationRepository;
        }

        // ==================== API 11: Get Notifications ====================
        public async Task<ApiResponse<NotificationListResponse>> GetNotificationsAsync(
            int userId, int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 50) pageSize = 50;

            var unreadCount = await _notificationRepository.GetUnreadCountAsync(userId);
            var (items, totalCount) = await _notificationRepository.GetByUserIdPagedAsync(userId, page, pageSize);

            var response = new NotificationListResponse
            {
                UnreadCount = unreadCount,
                Items = items.Select(n => new NotificationItemResponse
                {
                    NotificationId = n.Id,
                    Type = n.Type,
                    Title = n.Title,
                    Message = n.Message,
                    Data = n.Data,
                    IsRead = n.IsRead == true,
                    CreatedAt = n.CreatedAt
                }).ToList(),
                Pagination = new PaginationInfo
                {
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalItems = totalCount,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                    HasNext = page * pageSize < totalCount,
                    HasPrevious = page > 1
                }
            };

            return ApiResponse<NotificationListResponse>.SuccessResponse(response);
        }

        // ==================== API 12: Mark Notification as Read ====================
        public async Task<ApiResponse<object>> MarkAsReadAsync(int userId, int notificationId)
        {
            var notification = await _notificationRepository.GetByIdAndUserIdAsync(notificationId, userId);
            if (notification == null)
                return ApiResponse<object>.ErrorResponse("Notification not found.");

            await _notificationRepository.MarkAsReadAsync(notificationId);

            return ApiResponse<object>.SuccessResponse("Notification marked as read.");
        }

        // ==================== API 13: Mark All Notifications as Read ====================
        public async Task<ApiResponse<object>> MarkAllAsReadAsync(int userId)
        {
            await _notificationRepository.MarkAllAsReadAsync(userId);

            return ApiResponse<object>.SuccessResponse("All notifications marked as read.");
        }

        // ==================== API 14: Broadcast Notification (Admin) ====================
        public async Task<ApiResponse<object>> BroadcastAsync(BroadcastNotificationRequest request)
        {
            // Get all active customers (Role = 3)
            var customerIds = await _context.Users
                .Where(u => u.Role == 3 && u.IsActive == true)
                .Select(u => u.Id)
                .ToListAsync();

            if (!customerIds.Any())
                return ApiResponse<object>.ErrorResponse("No active customers found.");

            var notifications = customerIds.Select(customerId => new Notification
            {
                UserId = customerId,
                Type = request.Type,
                Title = request.Title,
                Message = request.Message,
                Data = request.Data,
                IsRead = false,
                CreatedAt = DateTime.Now
            }).ToList();

            await _notificationRepository.CreateRangeAsync(notifications);

            return ApiResponse<object>.SuccessResponse(new
            {
                sentCount = customerIds.Count
            }, $"Notification sent to {customerIds.Count} customers.");
        }

        // ==================== Get Unread Count ====================
        public async Task<ApiResponse<object>> GetUnreadCountAsync(int userId)
        {
            var count = await _notificationRepository.GetUnreadCountAsync(userId);
            return ApiResponse<object>.SuccessResponse(new { unreadCount = count });
        }

        // ==================== Delete Notification ====================
        public async Task<ApiResponse<object>> DeleteNotificationAsync(int userId, int notificationId)
        {
            var notification = await _notificationRepository.GetByIdAndUserIdAsync(notificationId, userId);
            if (notification == null)
                return ApiResponse<object>.ErrorResponse("Notification not found.");

            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();

            return ApiResponse<object>.SuccessResponse("Notification deleted successfully.");
        }
    }
}
