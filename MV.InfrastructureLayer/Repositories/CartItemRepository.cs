using Microsoft.EntityFrameworkCore;
using MV.DomainLayer.Entities;
using MV.InfrastructureLayer.DBContext;
using MV.InfrastructureLayer.Interfaces;

namespace MV.InfrastructureLayer.Repositories
{
    public class CartItemRepository : ICartItemRepository
    {
        private readonly FashionDbContext _context;

        public CartItemRepository(FashionDbContext context)
        {
            _context = context;
        }

        public async Task<List<CartItem>> GetByUserIdAsync(int userId)
        {
            return await _context.CartItems
                .Include(ci => ci.ProductVariant)
                    .ThenInclude(v => v.Product)
                        .ThenInclude(p => p.ProductImages.Where(img => img.IsPrimary == true))
                .Where(ci => ci.UserId == userId)
                .OrderByDescending(ci => ci.AddedAt)
                .ToListAsync();
        }

        public async Task<CartItem?> GetByIdAndUserIdAsync(int cartItemId, int userId)
        {
            return await _context.CartItems
                .Include(ci => ci.ProductVariant)
                .FirstOrDefaultAsync(ci => ci.Id == cartItemId && ci.UserId == userId);
        }

        public async Task<CartItem?> GetByUserIdAndVariantIdAsync(int userId, int productVariantId)
        {
            return await _context.CartItems
                .Include(ci => ci.ProductVariant)
                .FirstOrDefaultAsync(ci => ci.UserId == userId && ci.ProductVariantId == productVariantId);
        }

        public async Task<CartItem> CreateAsync(CartItem cartItem)
        {
            _context.CartItems.Add(cartItem);
            await _context.SaveChangesAsync();
            return cartItem;
        }

        public async Task UpdateAsync(CartItem cartItem)
        {
            _context.CartItems.Update(cartItem);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(CartItem cartItem)
        {
            _context.CartItems.Remove(cartItem);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAllByUserIdAsync(int userId)
        {
            await _context.CartItems
                .Where(ci => ci.UserId == userId)
                .ExecuteDeleteAsync();
        }
    }
}
