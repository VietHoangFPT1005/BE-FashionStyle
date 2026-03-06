using MV.DomainLayer.Entities;

namespace MV.InfrastructureLayer.Interfaces
{
    public interface IOrderItemRepository
    {
        Task CreateRangeAsync(List<OrderItem> orderItems);
        Task<List<OrderItem>> GetByOrderIdAsync(int orderId);
        Task<bool> HasUserPurchasedProductAsync(int userId, int productId);
    }
}
