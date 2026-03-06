using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.DTOs.Payment.Response;

namespace MV.ApplicationLayer.ServiceInterfaces
{
    public interface IPaymentService
    {
        Task<ApiResponse<SePayPaymentResponse>> CreateSePayPaymentAsync(int userId, int orderId);
        Task<ApiResponse<object>> HandleSePayCallbackAsync(string jsonBody);
        Task<ApiResponse<PaymentStatusResponse>> GetPaymentStatusAsync(int userId, int orderId);
    }
}
