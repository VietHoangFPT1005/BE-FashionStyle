using MV.DomainLayer.Entities;

namespace MV.InfrastructureLayer.Interfaces
{
    public interface IProductRepository
    {
        Task<Product?> GetByIdAsync(int id);
        Task<Product?> GetDetailByIdAsync(int id);
        Task<(List<Product> Items, int TotalCount)> GetProductsPagedAsync(
            int page, int pageSize,
            int? categoryId, string? gender, string? search,
            string? tags, decimal? minPrice, decimal? maxPrice,
            string sortBy, string sortOrder, bool? isFeatured,
            List<int>? categoryIds = null);
        Task<List<Product>> SearchProductsAsync(string keyword, int limit);
        Task IncrementViewCountAsync(int productId);
        Task<bool> ExistsAndActiveAsync(int productId);
    }
}
