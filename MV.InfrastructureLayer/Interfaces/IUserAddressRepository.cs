using MV.DomainLayer.Entities;

namespace MV.InfrastructureLayer.Interfaces
{
    public interface IUserAddressRepository
    {
        Task<List<UserAddress>> GetByUserIdAsync(int userId);
        Task<UserAddress?> GetByIdAndUserIdAsync(int addressId, int userId);
        Task<int> CountByUserIdAsync(int userId);
        Task<UserAddress> CreateAsync(UserAddress address);
        Task UpdateAsync(UserAddress address);
        Task DeleteAsync(UserAddress address);
        Task ResetDefaultAsync(int userId);
        Task SetDefaultAsync(int addressId, int userId);
    }
}
