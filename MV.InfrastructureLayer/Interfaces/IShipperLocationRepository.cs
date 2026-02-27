using MV.DomainLayer.Entities;

namespace MV.InfrastructureLayer.Interfaces
{
    public interface IShipperLocationRepository
    {
        Task<ShipperLocation> CreateAsync(ShipperLocation location);
        Task<ShipperLocation?> GetLatestByOrderIdAsync(int orderId, int shipperId);
    }
}
