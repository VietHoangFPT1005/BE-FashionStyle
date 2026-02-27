using Microsoft.EntityFrameworkCore;
using MV.DomainLayer.Entities;
using MV.InfrastructureLayer.DBContext;
using MV.InfrastructureLayer.Interfaces;

namespace MV.InfrastructureLayer.Repositories
{
    public class SizeGuideRepository : ISizeGuideRepository
    {
        private readonly FashionDbContext _context;

        public SizeGuideRepository(FashionDbContext context)
        {
            _context = context;
        }

        public async Task<List<SizeGuide>> GetByProductIdAsync(int productId)
        {
            return await _context.SizeGuides
                .Where(sg => sg.ProductId == productId)
                .OrderBy(sg => sg.SizeName)
                .ToListAsync();
        }

        public async Task<bool> ExistsByProductIdAsync(int productId)
        {
            return await _context.SizeGuides
                .AnyAsync(sg => sg.ProductId == productId);
        }
    }
}
