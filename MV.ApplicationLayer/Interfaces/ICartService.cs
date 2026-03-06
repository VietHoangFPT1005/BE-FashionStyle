using MV.DomainLayer.DTOs.Cart.Request;
using MV.DomainLayer.DTOs.Cart.Response;
using MV.DomainLayer.DTOs.Common;

namespace MV.ApplicationLayer.ServiceInterfaces
{
    public interface ICartService
    {
        Task<ApiResponse<CartResponse>> GetCartAsync(int userId);
        Task<ApiResponse<object>> AddToCartAsync(int userId, AddToCartRequest request);
        Task<ApiResponse<object>> UpdateCartItemAsync(int userId, int cartItemId, UpdateCartItemRequest request);
        Task<ApiResponse<object>> RemoveCartItemAsync(int userId, int cartItemId);
        Task<ApiResponse<object>> ClearCartAsync(int userId);
    }
}
