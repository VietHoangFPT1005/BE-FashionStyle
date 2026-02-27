using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.DTOs.Product.Response;
using MV.DomainLayer.DTOs.Review.Request;
using MV.DomainLayer.DTOs.Review.Response;

namespace MV.ApplicationLayer.ServiceInterfaces
{
    public interface IReviewService
    {
        Task<ApiResponse<CreateReviewResponse>> CreateReviewAsync(int userId, int productId, CreateReviewRequest request);
    }
}
