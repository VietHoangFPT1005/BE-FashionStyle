using MV.ApplicationLayer.ServiceInterfaces;
using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.DTOs.Wishlist.Response;
using MV.DomainLayer.Entities;
using MV.InfrastructureLayer.Interfaces;

namespace MV.ApplicationLayer.Services
{
    public class WishlistService : IWishlistService
    {
        private readonly IWishlistRepository _wishlistRepository;
        private readonly IProductRepository _productRepository;

        public WishlistService(
            IWishlistRepository wishlistRepository,
            IProductRepository productRepository)
        {
            _wishlistRepository = wishlistRepository;
            _productRepository = productRepository;
        }

        public async Task<ApiResponse<List<WishlistItemResponse>>> GetWishlistAsync(int userId)
        {
            var wishlists = await _wishlistRepository.GetByUserIdAsync(userId);

            var response = wishlists.Select(w => new WishlistItemResponse
            {
                ProductId = w.Product.Id,
                Name = w.Product.Name,
                Price = w.Product.Price,
                SalePrice = w.Product.SalePrice,
                PrimaryImage = w.Product.ProductImages
                    .FirstOrDefault(img => img.IsPrimary == true)?.ImageUrl,
                InStock = w.Product.ProductVariants
                    .Any(v => (v.StockQuantity ?? 0) > 0),
                AddedAt = w.CreatedAt
            }).ToList();

            return ApiResponse<List<WishlistItemResponse>>.SuccessResponse(response);
        }

        public async Task<ApiResponse<object>> AddToWishlistAsync(int userId, int productId)
        {
            // Check product exists and is active
            if (!await _productRepository.ExistsAndActiveAsync(productId))
                return ApiResponse<object>.ErrorResponse("Product not found.");

            // Check if already in wishlist
            if (await _wishlistRepository.ExistsAsync(userId, productId))
                return ApiResponse<object>.ErrorResponse("Product is already in your wishlist.");

            var wishlist = new Wishlist
            {
                UserId = userId,
                ProductId = productId,
                CreatedAt = DateTime.Now
            };

            await _wishlistRepository.CreateAsync(wishlist);
            return ApiResponse<object>.SuccessResponse("Product added to wishlist successfully.");
        }

        public async Task<ApiResponse<object>> RemoveFromWishlistAsync(int userId, int productId)
        {
            var deleted = await _wishlistRepository.DeleteAsync(userId, productId);
            if (!deleted)
                return ApiResponse<object>.ErrorResponse("Product not found in your wishlist.");

            return ApiResponse<object>.SuccessResponse("Product removed from wishlist successfully.");
        }
    }
}
