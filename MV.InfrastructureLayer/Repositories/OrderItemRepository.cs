using Microsoft.EntityFrameworkCore;
using MV.DomainLayer.Entities;
using MV.InfrastructureLayer.DBContext;
using MV.InfrastructureLayer.Interfaces;

namespace MV.InfrastructureLayer.Repositories
{
    public class OrderItemRepository : IOrderItemRepository
    {
        private readonly FashionDbContext _context;

        public OrderItemRepository(FashionDbContext context)
        {
            _context = context;
        }

        public async Task CreateRangeAsync(List<OrderItem> orderItems)
        {
            _context.OrderItems.AddRange(orderItems);
            await _context.SaveChangesAsync();
        }

        public async Task<List<OrderItem>> GetByOrderIdAsync(int orderId)
        {
            return await _context.OrderItems
                .Where(oi => oi.OrderId == orderId)
                .ToListAsync();
        }

        public async Task<bool> HasUserPurchasedProductAsync(int userId, int productId)
        {
            // Check if user has a DELIVERED order containing any variant of this product
            return await _context.OrderItems
                .Include(oi => oi.Order)
                .Include(oi => oi.ProductVariant)
                .AnyAsync(oi =>
                    oi.Order.UserId == userId &&
                    oi.Order.Status == "DELIVERED" &&
                    oi.ProductVariant != null &&
                    oi.ProductVariant.ProductId == productId);
        }
    }
}
