using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.DTOs.Order.Request;
using MV.DomainLayer.DTOs.Order.Response;

namespace MV.ApplicationLayer.ServiceInterfaces
{
    public interface IOrderService
    {
        // Customer
        Task<ApiResponse<CreateOrderResponse>> CreateOrderAsync(int userId, CreateOrderRequest request);
        Task<ApiResponse<PaginatedResponse<OrderListResponse>>> GetMyOrdersAsync(
            int userId, int page, int pageSize, string? status);
        Task<ApiResponse<OrderDetailResponse>> GetOrderDetailAsync(int orderId, int userId);
        Task<ApiResponse<object>> CancelOrderAsync(int orderId, int userId, CancelOrderRequest request);

        // Admin/Staff
        Task<ApiResponse<PaginatedResponse<AdminOrderListResponse>>> GetAllOrdersAsync(
            int page, int pageSize, string? status, string? search,
            DateTime? startDate, DateTime? endDate);
        Task<ApiResponse<OrderDetailResponse>> GetAdminOrderDetailAsync(int orderId);
        Task<ApiResponse<object>> ConfirmOrderAsync(int orderId);
        Task<ApiResponse<object>> AssignShipperAsync(int orderId, AssignShipperRequest request);
        Task<ApiResponse<object>> AdminCancelOrderAsync(int orderId, CancelOrderRequest request);
    }
}
