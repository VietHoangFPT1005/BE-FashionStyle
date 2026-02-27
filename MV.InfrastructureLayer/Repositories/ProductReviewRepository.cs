using Microsoft.EntityFrameworkCore;
using MV.DomainLayer.Entities;
using MV.InfrastructureLayer.DBContext;
using MV.InfrastructureLayer.Interfaces;

namespace MV.InfrastructureLayer.Repositories
{
    public class ProductReviewRepository : IProductReviewRepository
    {
        private readonly FashionDbContext _context;

        public ProductReviewRepository(FashionDbContext context)
        {
            _context = context;
        }

        public async Task<(List<ProductReview> Items, int TotalCount)> GetByProductIdPagedAsync(
            int productId, int page, int pageSize, int? rating, string sortBy)
        {
            var query = _context.ProductReviews
                .Include(r => r.User)
                .Where(r => r.ProductId == productId)
                .AsQueryable();

            if (rating.HasValue)
            {
                query = query.Where(r => r.Rating == rating.Value);
            }

            var totalCount = await query.CountAsync();

            query = sortBy.ToLower() switch
            {
                "rating" => query.OrderByDescending(r => r.Rating).ThenByDescending(r => r.CreatedAt),
                _ => query.OrderByDescending(r => r.CreatedAt)
            };

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<Dictionary<int, int>> GetRatingDistributionAsync(int productId)
        {
            var distribution = await _context.ProductReviews
                .Where(r => r.ProductId == productId)
                .GroupBy(r => r.Rating)
                .Select(g => new { Rating = g.Key, Count = g.Count() })
                .ToListAsync();

            // Ensure all ratings 1-5 are present
            var result = new Dictionary<int, int>();
            for (int i = 1; i <= 5; i++)
            {
                result[i] = distribution.FirstOrDefault(d => d.Rating == i)?.Count ?? 0;
            }
            return result;
        }
    }
}
