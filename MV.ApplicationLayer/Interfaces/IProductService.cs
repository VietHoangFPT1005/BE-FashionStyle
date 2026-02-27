using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.DTOs.Product.Response;

namespace MV.ApplicationLayer.ServiceInterfaces
{
    public interface IProductService
    {
        Task<ApiResponse<PaginatedResponse<ProductListResponse>>> GetProductsAsync(
            int page, int pageSize,
            int? categoryId, string? gender, string? search,
            string? tags, decimal? minPrice, decimal? maxPrice,
            string sortBy, string sortOrder, bool? isFeatured);

        Task<ApiResponse<List<ProductSearchResponse>>> SearchProductsAsync(string keyword, int limit);

        Task<ApiResponse<PaginatedResponse<ProductListResponse>>> GetProductsByCategoryAsync(
            int categoryId, int page, int pageSize,
            string? gender, string? search,
            string? tags, decimal? minPrice, decimal? maxPrice,
            string sortBy, string sortOrder, bool? isFeatured);

        Task<ApiResponse<ProductDetailResponse>> GetProductDetailAsync(int productId);
        Task<ApiResponse<VariantListResponse>> GetProductVariantsAsync(int productId);
        Task<ApiResponse<List<ProductImageResponse>>> GetProductImagesAsync(int productId);
        Task<ApiResponse<List<SizeGuideResponse>>> GetSizeGuideAsync(int productId);
        Task<ApiResponse<RecommendSizeResponse>> RecommendSizeAsync(int productId, int userId);
        Task<ApiResponse<ProductReviewListResponse>> GetProductReviewsAsync(
            int productId, int page, int pageSize, int? rating, string sortBy);
    }
}
