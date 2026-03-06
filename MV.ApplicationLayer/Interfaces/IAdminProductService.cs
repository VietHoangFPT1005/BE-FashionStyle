using MV.DomainLayer.DTOs.Admin.Request;
using MV.DomainLayer.DTOs.Admin.Response;
using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.DTOs.Voucher.Response;

namespace MV.ApplicationLayer.ServiceInterfaces
{
    public interface IAdminProductService
    {
        // ==================== Product CRUD ====================
        Task<ApiResponse<PaginatedResponse<AdminProductResponse>>> GetProductsAsync(
            int page, int pageSize, string? search, int? categoryId, bool? isActive);
        Task<ApiResponse<AdminProductDetailResponse>> GetProductDetailAsync(int productId);
        Task<ApiResponse<AdminProductDetailResponse>> CreateProductAsync(CreateProductRequest request);
        Task<ApiResponse<AdminProductDetailResponse>> UpdateProductAsync(int productId, UpdateProductRequest request);
        Task<ApiResponse<object>> DeleteProductAsync(int productId);

        // ==================== Variant CRUD ====================
        Task<ApiResponse<AdminVariantResponse>> CreateVariantAsync(int productId, CreateVariantRequest request);
        Task<ApiResponse<AdminVariantResponse>> UpdateVariantAsync(int variantId, UpdateVariantRequest request);
        Task<ApiResponse<object>> DeleteVariantAsync(int variantId);

        // ==================== Image CRUD ====================
        Task<ApiResponse<AdminProductImageResponse>> CreateProductImageAsync(int productId, CreateProductImageRequest request);
        Task<ApiResponse<object>> DeleteProductImageAsync(int imageId);

        // ==================== Size Guide ====================
        Task<ApiResponse<object>> UpsertSizeGuideAsync(int productId, CreateSizeGuideRequest request);

        // ==================== Category CRUD ====================
        Task<ApiResponse<object>> CreateCategoryAsync(CreateCategoryRequest request);
        Task<ApiResponse<object>> UpdateCategoryAsync(int categoryId, UpdateCategoryRequest request);
        Task<ApiResponse<object>> DeleteCategoryAsync(int categoryId);

        // ==================== Voucher CRUD ====================
        Task<ApiResponse<PaginatedResponse<VoucherResponse>>> GetVouchersAsync(
            int page, int pageSize, bool? isActive);
        Task<ApiResponse<VoucherResponse>> CreateVoucherAsync(CreateVoucherRequest request);
        Task<ApiResponse<VoucherResponse>> UpdateVoucherAsync(int voucherId, UpdateVoucherRequest request);
        Task<ApiResponse<object>> DeleteVoucherAsync(int voucherId);
    }
}
