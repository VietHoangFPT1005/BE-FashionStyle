using Microsoft.EntityFrameworkCore;
using MV.DomainLayer.Entities;
using MV.InfrastructureLayer.DBContext;
using MV.InfrastructureLayer.Interfaces;

namespace MV.InfrastructureLayer.Repositories
{
    public class CategoryRepository : ICategoryRepository
    {
        private readonly FashionDbContext _context;

        public CategoryRepository(FashionDbContext context)
        {
            _context = context;
        }

        public async Task<List<Category>> GetAllActiveWithProductCountAsync()
        {
            return await _context.Categories
                .Where(c => c.IsActive == true)
                .Include(c => c.InverseParent.Where(child => child.IsActive == true))
                .Include(c => c.Products.Where(p => p.IsActive == true && p.IsDeleted == false))
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<Category?> GetByIdAsync(int id)
        {
            return await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == id && c.IsActive == true);
        }

        public async Task<List<int>> GetChildCategoryIdsAsync(int parentId)
        {
            return await _context.Categories
                .Where(c => c.ParentId == parentId && c.IsActive == true)
                .Select(c => c.Id)
                .ToListAsync();
        }
    }
}
