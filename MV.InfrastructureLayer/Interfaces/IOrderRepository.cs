using MV.DomainLayer.Entities;

namespace MV.InfrastructureLayer.Interfaces
{
    public interface IOrderRepository
    {
        Task<Order> CreateAsync(Order order);
        Task UpdateAsync(Order order);
        Task<Order?> GetByIdAsync(int orderId);
        Task<Order?> GetByIdWithDetailsAsync(int orderId);
        Task<Order?> GetByIdAndUserIdAsync(int orderId, int userId);
        Task<Order?> GetByIdAndUserIdWithDetailsAsync(int orderId, int userId);
        Task<Order?> GetByOrderCodeAsync(string orderCode);
        Task<(List<Order> Items, int TotalCount)> GetByUserIdPagedAsync(
            int userId, int page, int pageSize, string? status);
        Task<(List<Order> Items, int TotalCount)> GetAllOrdersPagedAsync(
            int page, int pageSize, string? status, string? search,
            DateTime? startDate, DateTime? endDate);
        Task<int> GetTodayOrderCountAsync();
    }
}
