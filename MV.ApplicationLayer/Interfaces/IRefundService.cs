using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.DTOs.Refund.Request;
using MV.DomainLayer.DTOs.Refund.Response;

namespace MV.ApplicationLayer.ServiceInterfaces
{
    public interface IRefundService
    {
        Task<ApiResponse<RefundResponse>> RequestRefundAsync(int userId, int orderId, CreateRefundRequest request);
        Task<ApiResponse<RefundResponse>> GetRefundByOrderAsync(int userId, int orderId);
        Task<ApiResponse<PaginatedResponse<RefundResponse>>> GetAllRefundsAsync(int page, int pageSize, string? status);
        Task<ApiResponse<RefundResponse>> ApproveRefundAsync(int adminId, int refundId, ProcessRefundRequest request);
        Task<ApiResponse<RefundResponse>> RejectRefundAsync(int adminId, int refundId, ProcessRefundRequest request);
    }
}
