using MV.DomainLayer.Entities;

namespace MV.InfrastructureLayer.Interfaces
{
    public interface IVoucherRepository
    {
        Task<Voucher?> GetByCodeAsync(string code);
    }
}
