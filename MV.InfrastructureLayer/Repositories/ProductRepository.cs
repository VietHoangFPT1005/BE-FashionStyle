using Microsoft.EntityFrameworkCore;
using MV.DomainLayer.Entities;
using MV.InfrastructureLayer.DBContext;
using MV.InfrastructureLayer.Interfaces;

namespace MV.InfrastructureLayer.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly FashionDbContext _context;

        public ProductRepository(FashionDbContext context)
        {
            _context = context;
        }

        public async Task<Product?> GetByIdAsync(int id)
        {
            return await _context.Products
                .FirstOrDefaultAsync(p => p.Id == id && p.IsActive == true && p.IsDeleted == false);
        }

        public async Task<Product?> GetDetailByIdAsync(int id)
        {
            return await _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductImages.OrderBy(img => img.SortOrder))
                .Include(p => p.ProductVariants.Where(v => v.IsActive == true))
                .Include(p => p.SizeGuides)
                .FirstOrDefaultAsync(p => p.Id == id && p.IsActive == true && p.IsDeleted == false);
        }

        public async Task<(List<Product> Items, int TotalCount)> GetProductsPagedAsync(
            int page, int pageSize,
            int? categoryId, string? gender, string? search,
            string? tags, decimal? minPrice, decimal? maxPrice,
            string sortBy, string sortOrder, bool? isFeatured,
            List<int>? categoryIds = null)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductImages.Where(img => img.IsPrimary == true))
                .Include(p => p.ProductVariants.Where(v => v.IsActive == true && v.StockQuantity > 0))
                .Where(p => p.IsActive == true && p.IsDeleted == false)
                .AsQueryable();

            // Filter by category (single or multiple for parent+children)
            if (categoryIds != null && categoryIds.Count > 0)
            {
                query = query.Where(p => p.CategoryId.HasValue && categoryIds.Contains(p.CategoryId.Value));
            }
            else if (categoryId.HasValue)
            {
                query = query.Where(p => p.CategoryId == categoryId.Value);
            }

            // Filter by gender
            if (!string.IsNullOrWhiteSpace(gender))
            {
                query = query.Where(p => p.Gender == gender);
            }

            // Filter by search keyword
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(p => p.Name.ToLower().Contains(searchLower)
                    || (p.Description != null && p.Description.ToLower().Contains(searchLower))
                    || (p.BrandName != null && p.BrandName.ToLower().Contains(searchLower)));
            }

            // Filter by tags (PostgreSQL array contains)
            if (!string.IsNullOrWhiteSpace(tags))
            {
                var tagList = tags.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList();
                foreach (var tag in tagList)
                {
                    query = query.Where(p => p.Tags != null && p.Tags.Contains(tag));
                }
            }

            // Filter by price range (use SalePrice if available, otherwise Price)
            if (minPrice.HasValue)
            {
                query = query.Where(p => (p.SalePrice ?? p.Price) >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                query = query.Where(p => (p.SalePrice ?? p.Price) <= maxPrice.Value);
            }

            // Filter by isFeatured
            if (isFeatured.HasValue)
            {
                query = query.Where(p => p.IsFeatured == isFeatured.Value);
            }

            var totalCount = await query.CountAsync();

            // Sort
            query = sortBy.ToLower() switch
            {
                "price" => sortOrder.ToLower() == "asc"
                    ? query.OrderBy(p => p.SalePrice ?? p.Price)
                    : query.OrderByDescending(p => p.SalePrice ?? p.Price),
                "name" => sortOrder.ToLower() == "asc"
                    ? query.OrderBy(p => p.Name)
                    : query.OrderByDescending(p => p.Name),
                "soldcount" => sortOrder.ToLower() == "asc"
                    ? query.OrderBy(p => p.SoldCount ?? 0)
                    : query.OrderByDescending(p => p.SoldCount ?? 0),
                _ => sortOrder.ToLower() == "asc"
                    ? query.OrderBy(p => p.CreatedAt)
                    : query.OrderByDescending(p => p.CreatedAt)
            };

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<List<Product>> SearchProductsAsync(string keyword, int limit)
        {
            var keywordLower = keyword.ToLower();
            return await _context.Products
                .Include(p => p.ProductImages.Where(img => img.IsPrimary == true))
                .Where(p => p.IsActive == true && p.IsDeleted == false)
                .Where(p => p.Name.ToLower().Contains(keywordLower)
                    || (p.Description != null && p.Description.ToLower().Contains(keywordLower))
                    || (p.BrandName != null && p.BrandName.ToLower().Contains(keywordLower)))
                .OrderByDescending(p => p.SoldCount ?? 0)
                .Take(limit)
                .ToListAsync();
        }

        public async Task IncrementViewCountAsync(int productId)
        {
            await _context.Products
                .Where(p => p.Id == productId)
                .ExecuteUpdateAsync(s => s.SetProperty(
                    p => p.ViewCount, p => (p.ViewCount ?? 0) + 1));
        }

        public async Task<bool> ExistsAndActiveAsync(int productId)
        {
            return await _context.Products
                .AnyAsync(p => p.Id == productId && p.IsActive == true && p.IsDeleted == false);
        }
    }
}
