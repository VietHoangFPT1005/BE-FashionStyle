using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MV.ApplicationLayer.ServiceInterfaces;
using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.DTOs.Review.Request;
using MV.DomainLayer.DTOs.Review.Response;
using MV.DomainLayer.Entities;
using MV.InfrastructureLayer.DBContext;
using MV.InfrastructureLayer.Interfaces;

namespace MV.ApplicationLayer.Services
{
    public class ReviewService : IReviewService
    {
        private readonly FashionDbContext _context;
        private readonly IProductRepository _productRepository;
        private readonly IOrderItemRepository _orderItemRepository;
        private readonly IProductReviewRepository _reviewRepository;
        private readonly ILogger<ReviewService> _logger;

        public ReviewService(
            FashionDbContext context,
            IProductRepository productRepository,
            IOrderItemRepository orderItemRepository,
            IProductReviewRepository reviewRepository,
            ILogger<ReviewService> logger)
        {
            _context = context;
            _productRepository = productRepository;
            _orderItemRepository = orderItemRepository;
            _reviewRepository = reviewRepository;
            _logger = logger;
        }

        public async Task<ApiResponse<CreateReviewResponse>> CreateReviewAsync(
            int userId, int productId, CreateReviewRequest request)
        {
            // Check product exists
            if (!await _productRepository.ExistsAndActiveAsync(productId))
                return ApiResponse<CreateReviewResponse>.ErrorResponse("Product not found.");

            // Check user has purchased this product and order is DELIVERED
            var hasPurchased = await _orderItemRepository.HasUserPurchasedProductAsync(userId, productId);
            if (!hasPurchased)
                return ApiResponse<CreateReviewResponse>.ErrorResponse(
                    "You have not purchased this product or your order has not been delivered yet.");

            // Check if user already reviewed this product
            var existingReview = await _context.ProductReviews
                .AnyAsync(r => r.ProductId == productId && r.UserId == userId);
            if (existingReview)
                return ApiResponse<CreateReviewResponse>.ErrorResponse(
                    "You have already reviewed this product.");

            // Find the orderId for this review (latest delivered order with this product)
            var orderItem = await _context.OrderItems
                .Include(oi => oi.Order)
                .Include(oi => oi.ProductVariant)
                .Where(oi =>
                    oi.Order.UserId == userId &&
                    oi.Order.Status == "DELIVERED" &&
                    oi.ProductVariant != null &&
                    oi.ProductVariant.ProductId == productId)
                .OrderByDescending(oi => oi.Order.DeliveredAt)
                .FirstOrDefaultAsync();

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Create review
                var review = new ProductReview
                {
                    ProductId = productId,
                    UserId = userId,
                    OrderId = orderItem?.OrderId,
                    Rating = request.Rating,
                    Comment = request.Comment,
                    ReviewImageUrl = request.ReviewImageUrl,
                    HeightCm = request.HeightCm,
                    WeightKg = request.WeightKg,
                    SizeOrdered = request.SizeOrdered,
                    ShowBodyInfo = request.ShowBodyInfo,
                    CreatedAt = DateTime.Now
                };

                _context.ProductReviews.Add(review);
                await _context.SaveChangesAsync();

                // Update Product's AverageRating and TotalReviews
                var avgRating = await _context.ProductReviews
                    .Where(r => r.ProductId == productId)
                    .AverageAsync(r => (decimal)r.Rating);
                var totalReviews = await _context.ProductReviews
                    .CountAsync(r => r.ProductId == productId);

                await _context.Products
                    .Where(p => p.Id == productId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(p => p.AverageRating, Math.Round(avgRating, 1))
                        .SetProperty(p => p.TotalReviews, totalReviews));

                await transaction.CommitAsync();

                var response = new CreateReviewResponse
                {
                    ReviewId = review.Id,
                    ProductId = productId,
                    Rating = review.Rating,
                    Comment = review.Comment,
                    BodyInfo = request.ShowBodyInfo ? new ReviewBodyInfoResponse
                    {
                        HeightCm = request.HeightCm,
                        WeightKg = request.WeightKg,
                        SizeOrdered = request.SizeOrdered
                    } : null,
                    CreatedAt = review.CreatedAt
                };

                return ApiResponse<CreateReviewResponse>.SuccessResponse(response, "Review submitted successfully.");
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // ==================== Update Review ====================
        public async Task<ApiResponse<CreateReviewResponse>> UpdateReviewAsync(
            int userId, int productId, int reviewId, UpdateReviewRequest request)
        {
            var review = await _context.ProductReviews
                .FirstOrDefaultAsync(r => r.Id == reviewId && r.ProductId == productId && r.UserId == userId);

            if (review == null)
                return ApiResponse<CreateReviewResponse>.ErrorResponse("Review not found.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (request.Rating.HasValue) review.Rating = request.Rating.Value;
                if (request.Comment != null) review.Comment = request.Comment;
                if (request.ReviewImageUrl != null) review.ReviewImageUrl = request.ReviewImageUrl;
                if (request.ShowBodyInfo.HasValue) review.ShowBodyInfo = request.ShowBodyInfo.Value;

                await _context.SaveChangesAsync();

                // Recalculate product rating
                await RecalculateProductRatingAsync(productId);

                await transaction.CommitAsync();

                var response = new CreateReviewResponse
                {
                    ReviewId = review.Id,
                    ProductId = productId,
                    Rating = review.Rating,
                    Comment = review.Comment,
                    BodyInfo = review.ShowBodyInfo == true ? new ReviewBodyInfoResponse
                    {
                        HeightCm = review.HeightCm,
                        WeightKg = review.WeightKg,
                        SizeOrdered = review.SizeOrdered
                    } : null,
                    CreatedAt = review.CreatedAt
                };

                return ApiResponse<CreateReviewResponse>.SuccessResponse(response, "Review updated successfully.");
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // ==================== Delete Review (User) ====================
        public async Task<ApiResponse<object>> DeleteReviewAsync(int userId, int productId, int reviewId)
        {
            var review = await _context.ProductReviews
                .FirstOrDefaultAsync(r => r.Id == reviewId && r.ProductId == productId && r.UserId == userId);

            if (review == null)
                return ApiResponse<object>.ErrorResponse("Review not found.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.ProductReviews.Remove(review);
                await _context.SaveChangesAsync();

                await RecalculateProductRatingAsync(productId);

                await transaction.CommitAsync();
                return ApiResponse<object>.SuccessResponse("Review deleted successfully.");
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // ==================== Delete Review (Admin) ====================
        public async Task<ApiResponse<object>> AdminDeleteReviewAsync(int reviewId)
        {
            var review = await _context.ProductReviews.FindAsync(reviewId);
            if (review == null)
                return ApiResponse<object>.ErrorResponse("Review not found.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var productId = review.ProductId;
                _context.ProductReviews.Remove(review);
                await _context.SaveChangesAsync();

                await RecalculateProductRatingAsync(productId);

                await transaction.CommitAsync();

                _logger.LogWarning("AUDIT: Admin deleted Review {ReviewId} for Product {ProductId} at {Time}", reviewId, productId, DateTime.UtcNow);

                return ApiResponse<object>.SuccessResponse("Review deleted successfully.");
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task RecalculateProductRatingAsync(int productId)
        {
            var reviews = await _context.ProductReviews
                .Where(r => r.ProductId == productId)
                .ToListAsync();

            if (reviews.Any())
            {
                var avgRating = Math.Round((decimal)reviews.Average(r => r.Rating), 1);
                await _context.Products
                    .Where(p => p.Id == productId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(p => p.AverageRating, avgRating)
                        .SetProperty(p => p.TotalReviews, reviews.Count));
            }
            else
            {
                await _context.Products
                    .Where(p => p.Id == productId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(p => p.AverageRating, (decimal?)0)
                        .SetProperty(p => p.TotalReviews, 0));
            }
        }
    }
}
