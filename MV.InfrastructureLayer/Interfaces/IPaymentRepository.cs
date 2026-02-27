using MV.DomainLayer.Entities;

namespace MV.InfrastructureLayer.Interfaces
{
    public interface IPaymentRepository
    {
        Task<Payment> CreateAsync(Payment payment);
        Task UpdateAsync(Payment payment);
        Task<Payment?> GetByOrderIdAsync(int orderId);
        Task<Payment?> GetByOrderCodeAsync(string orderCode);
    }
}
