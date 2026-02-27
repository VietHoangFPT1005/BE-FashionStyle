using Microsoft.EntityFrameworkCore;
using MV.DomainLayer.Entities;
using MV.InfrastructureLayer.DBContext;
using MV.InfrastructureLayer.Interfaces;

namespace MV.InfrastructureLayer.Repositories
{
    public class ProductVariantRepository : IProductVariantRepository
    {
        private readonly FashionDbContext _context;

        public ProductVariantRepository(FashionDbContext context)
        {
            _context = context;
        }

        public async Task<List<ProductVariant>> GetByProductIdAsync(int productId)
        {
            return await _context.ProductVariants
                .Where(v => v.ProductId == productId && v.IsActive == true)
                .OrderBy(v => v.Size)
                .ThenBy(v => v.Color)
                .ToListAsync();
        }

        public async Task<ProductVariant?> GetByIdAsync(int id)
        {
            return await _context.ProductVariants
                .Include(v => v.Product)
                .FirstOrDefaultAsync(v => v.Id == id);
        }

        public async Task<bool> ExistsAndActiveAsync(int variantId)
        {
            return await _context.ProductVariants
                .AnyAsync(v => v.Id == variantId && v.IsActive == true);
        }
    }
}
