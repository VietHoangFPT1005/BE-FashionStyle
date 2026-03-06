using Microsoft.EntityFrameworkCore;
using MV.DomainLayer.Entities;
using MV.InfrastructureLayer.DBContext;
using MV.InfrastructureLayer.Interfaces;

namespace MV.InfrastructureLayer.Repositories
{
    public class ProductImageRepository : IProductImageRepository
    {
        private readonly FashionDbContext _context;

        public ProductImageRepository(FashionDbContext context)
        {
            _context = context;
        }

        public async Task<List<ProductImage>> GetByProductIdAsync(int productId)
        {
            return await _context.ProductImages
                .Where(img => img.ProductId == productId)
                .OrderBy(img => img.SortOrder)
                .ToListAsync();
        }

        public async Task<string?> GetPrimaryImageUrlAsync(int productId)
        {
            return await _context.ProductImages
                .Where(img => img.ProductId == productId && img.IsPrimary == true)
                .Select(img => img.ImageUrl)
                .FirstOrDefaultAsync();
        }
    }
}
