// =====================================================================
// TODO: Uncomment sau khi chạy SQL script + scaffold lại database
// =====================================================================
using MV.DomainLayer.Entities;

namespace MV.InfrastructureLayer.Interfaces
{
    public interface ISepayTransactionRepository
    {
        Task<SepayTransaction> CreateAsync(SepayTransaction transaction);
        Task UpdateAsync(SepayTransaction transaction);
        Task<bool> ExistsBySepayIdAsync(string sepayId);
    }
}
