using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            FashionDbContext context,
            INotificationRepository notificationRepository,
            ILogger<NotificationService> logger)
        {
            _context = context;
            _notificationRepository = notificationRepository;
            _logger = logger;
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

        // ==================== Business Event Senders ====================

        public async Task SendOrderStatusChangedAsync(int userId, int orderId, string orderCode, string newStatus, string? reason = null)
        {
            var (title, message) = newStatus switch
            {
                "CONFIRMED" => ("Đơn hàng đã xác nhận", $"Đơn hàng {orderCode} đã được xác nhận và đang chuẩn bị."),
                "PROCESSING" => ("Đơn hàng đang xử lý", $"Đơn hàng {orderCode} đang được chuẩn bị giao cho shipper."),
                "SHIPPING" => ("Đơn hàng đang giao", $"Đơn hàng {orderCode} đang được giao đến bạn."),
                "DELIVERED" => ("Giao hàng thành công", $"Đơn hàng {orderCode} đã được giao thành công."),
                "CANCELLED" => ("Đơn hàng đã hủy", $"Đơn hàng {orderCode} đã bị hủy.{(reason != null ? $" Lý do: {reason}" : "")}"),
                _ => ("Cập nhật đơn hàng", $"Đơn hàng {orderCode} đã được cập nhật trạng thái: {newStatus}.")
            };

            await CreateNotificationSafe(userId, "ORDER", title, message, orderId);
        }

        public async Task SendPaymentConfirmedAsync(int userId, int orderId, string orderCode, decimal amount)
        {
            await CreateNotificationSafe(
                userId, "PAYMENT",
                "Thanh toán thành công",
                $"Thanh toán đơn hàng {orderCode} đã được xác nhận. Số tiền: {amount:N0}₫",
                orderId);
        }

        public async Task SendPaymentExpiredAsync(int userId, int orderId, string orderCode)
        {
            await CreateNotificationSafe(
                userId, "PAYMENT",
                "Thanh toán hết hạn",
                $"Đơn hàng {orderCode} đã bị hủy do hết thời gian thanh toán. Vui lòng đặt lại đơn hàng mới.",
                orderId);
        }

        public async Task SendShippingUpdateAsync(int userId, int orderId, string orderCode, string updateType, string? detail = null)
        {
            var (title, message, screen) = updateType switch
            {
                "PICKUP" => ("Đơn hàng đang giao", $"Đơn hàng {orderCode} đã được shipper lấy hàng và đang giao đến bạn.", "ORDER_TRACKING"),
                "DELIVERY_FAILED" => ("Giao hàng không thành công", $"Giao hàng đơn {orderCode} không thành công.{(detail != null ? $" {detail}" : "")} Sẽ giao lại.", "ORDER_TRACKING"),
                "DELIVERY_CANCELLED" => ("Đơn hàng đã hủy - giao hàng thất bại", $"Đơn hàng {orderCode} đã bị hủy do giao hàng thất bại nhiều lần.", "ORDER_DETAIL"),
                "DELIVERED" => ("Giao hàng thành công", $"Đơn hàng {orderCode} đã được giao thành công.", "ORDER_DETAIL"),
                _ => ("Cập nhật giao hàng", $"Đơn hàng {orderCode}: {detail ?? updateType}", "ORDER_DETAIL")
            };

            await CreateNotificationSafe(
                userId, "SHIPPING", title, message, orderId, screen);
        }

        // ==================== Private Helper ====================

        /// <summary>
        /// Create notification with error handling - never throws, logs error instead.
        /// Prevents notification failures from breaking business transactions.
        /// </summary>
        private async Task CreateNotificationSafe(int userId, string type, string title, string message, int orderId, string screen = "ORDER_DETAIL")
        {
            try
            {
                await _notificationRepository.CreateAsync(new Notification
                {
                    UserId = userId,
                    Type = type,
                    Title = title,
                    Message = message,
                    Data = $"{{\"orderId\":{orderId},\"screen\":\"{screen}\"}}",
                    IsRead = false,
                    CreatedAt = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create notification [{Type}] for User {UserId}, Order {OrderId}", type, userId, orderId);
            }
        }
    }
}
