using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.DTOs.Wishlist.Response;

namespace MV.ApplicationLayer.ServiceInterfaces
{
    public interface IWishlistService
    {
        Task<ApiResponse<List<WishlistItemResponse>>> GetWishlistAsync(int userId);
        Task<ApiResponse<object>> AddToWishlistAsync(int userId, int productId);
        Task<ApiResponse<object>> RemoveFromWishlistAsync(int userId, int productId);
    }
}
