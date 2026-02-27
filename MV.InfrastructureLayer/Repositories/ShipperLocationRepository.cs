using Microsoft.EntityFrameworkCore;
using MV.DomainLayer.Entities;
using MV.InfrastructureLayer.DBContext;
using MV.InfrastructureLayer.Interfaces;

namespace MV.InfrastructureLayer.Repositories
{
    public class ShipperLocationRepository : IShipperLocationRepository
    {
        private readonly FashionDbContext _context;

        public ShipperLocationRepository(FashionDbContext context)
        {
            _context = context;
        }

        public async Task<ShipperLocation> CreateAsync(ShipperLocation location)
        {
            _context.ShipperLocations.Add(location);
            await _context.SaveChangesAsync();
            return location;
        }

        public async Task<ShipperLocation?> GetLatestByOrderIdAsync(int orderId, int shipperId)
        {
            return await _context.ShipperLocations
                .Where(sl => sl.OrderId == orderId && sl.ShipperId == shipperId)
                .OrderByDescending(sl => sl.CreatedAt)
                .FirstOrDefaultAsync();
        }
    }
}
