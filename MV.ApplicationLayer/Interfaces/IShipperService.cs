using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.DTOs.Shipper.Request;
using MV.DomainLayer.DTOs.Shipper.Response;

namespace MV.ApplicationLayer.ServiceInterfaces
{
    public interface IShipperService
    {
        Task<ApiResponse<List<ShipperOrderListResponse>>> GetShipperOrdersAsync(int shipperId, string? status);
        Task<ApiResponse<object>> PickupOrderAsync(int shipperId, int orderId);
        Task<ApiResponse<object>> DeliverOrderAsync(int shipperId, int orderId);
        Task<ApiResponse<object>> DeliveryFailedAsync(int shipperId, int orderId, DeliveryFailedRequest request);
        Task<ApiResponse<object>> UpdateLocationAsync(int shipperId, UpdateLocationRequest request);
        Task<ApiResponse<TrackingResponse>> TrackOrderAsync(int userId, int orderId);
    }
}
