using MV.DomainLayer.Entities;

namespace MV.InfrastructureLayer.Interfaces
{
    public interface IProductReviewRepository
    {
        Task<(List<ProductReview> Items, int TotalCount)> GetByProductIdPagedAsync(
            int productId, int page, int pageSize, int? rating, string sortBy);
        Task<Dictionary<int, int>> GetRatingDistributionAsync(int productId);
    }
}
