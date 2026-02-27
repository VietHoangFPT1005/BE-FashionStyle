using Microsoft.EntityFrameworkCore;
using MV.DomainLayer.Entities;
using MV.InfrastructureLayer.DBContext;
using MV.InfrastructureLayer.Interfaces;

namespace MV.InfrastructureLayer.Repositories
{
    public class WishlistRepository : IWishlistRepository
    {
        private readonly FashionDbContext _context;

        public WishlistRepository(FashionDbContext context)
        {
            _context = context;
        }

        public async Task<List<Wishlist>> GetByUserIdAsync(int userId)
        {
            return await _context.Wishlists
                .Include(w => w.Product)
                    .ThenInclude(p => p.ProductImages.Where(img => img.IsPrimary == true))
                .Include(w => w.Product)
                    .ThenInclude(p => p.ProductVariants.Where(v => v.IsActive == true && v.StockQuantity > 0))
                .Where(w => w.UserId == userId && w.Product.IsActive == true && w.Product.IsDeleted == false)
                .OrderByDescending(w => w.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> ExistsAsync(int userId, int productId)
        {
            return await _context.Wishlists
                .AnyAsync(w => w.UserId == userId && w.ProductId == productId);
        }

        public async Task<Wishlist> CreateAsync(Wishlist wishlist)
        {
            _context.Wishlists.Add(wishlist);
            await _context.SaveChangesAsync();
            return wishlist;
        }

        public async Task<bool> DeleteAsync(int userId, int productId)
        {
            var count = await _context.Wishlists
                .Where(w => w.UserId == userId && w.ProductId == productId)
                .ExecuteDeleteAsync();
            return count > 0;
        }
    }
}
