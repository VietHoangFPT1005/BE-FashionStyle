using Microsoft.EntityFrameworkCore;
using MV.DomainLayer.Entities;
using MV.InfrastructureLayer.DBContext;
using MV.InfrastructureLayer.Interfaces;

namespace MV.InfrastructureLayer.Repositories
{
    public class OrderRepository : IOrderRepository
    {
        private readonly FashionDbContext _context;

        public OrderRepository(FashionDbContext context)
        {
            _context = context;
        }

        public async Task<Order> CreateAsync(Order order)
        {
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();
            return order;
        }

        public async Task UpdateAsync(Order order)
        {
            _context.Orders.Update(order);
            await _context.SaveChangesAsync();
        }

        public async Task<Order?> GetByIdAsync(int orderId)
        {
            return await _context.Orders
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.Id == orderId);
        }

        public async Task<Order?> GetByIdWithDetailsAsync(int orderId)
        {
            return await _context.Orders
                .Include(o => o.OrderItems)
                .Include(o => o.Payment)
                .Include(o => o.User)
                .Include(o => o.Shipper)
                .FirstOrDefaultAsync(o => o.Id == orderId);
        }

        public async Task<Order?> GetByIdAndUserIdAsync(int orderId, int userId)
        {
            return await _context.Orders
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);
        }

        public async Task<Order?> GetByIdAndUserIdWithDetailsAsync(int orderId, int userId)
        {
            return await _context.Orders
                .Include(o => o.OrderItems)
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);
        }

        public async Task<Order?> GetByOrderCodeAsync(string orderCode)
        {
            return await _context.Orders
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.OrderCode == orderCode);
        }

        public async Task<(List<Order> Items, int TotalCount)> GetByUserIdPagedAsync(
            int userId, int page, int pageSize, string? status)
        {
            var query = _context.Orders
                .Include(o => o.OrderItems)
                .Include(o => o.Payment)
                .Where(o => o.UserId == userId)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(o => o.Status == status);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<(List<Order> Items, int TotalCount)> GetAllOrdersPagedAsync(
            int page, int pageSize, string? status, string? search,
            DateTime? startDate, DateTime? endDate)
        {
            var query = _context.Orders
                .Include(o => o.Payment)
                .Include(o => o.User)
                .Include(o => o.Shipper)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(o => o.Status == status);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(o =>
                    o.OrderCode.ToLower().Contains(searchLower) ||
                    o.ShippingPhone.Contains(search));
            }

            if (startDate.HasValue)
                query = query.Where(o => o.CreatedAt >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(o => o.CreatedAt <= endDate.Value.AddDays(1));

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<int> GetTodayOrderCountAsync()
        {
            var today = DateTime.Now.Date;
            return await _context.Orders
                .CountAsync(o => o.CreatedAt >= today);
        }
    }
}
