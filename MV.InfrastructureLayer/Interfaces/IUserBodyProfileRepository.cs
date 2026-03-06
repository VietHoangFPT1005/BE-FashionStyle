using MV.DomainLayer.Entities;

namespace MV.InfrastructureLayer.Interfaces
{
    public interface IUserBodyProfileRepository
    {
        Task<UserBodyProfile?> GetByUserIdAsync(int userId);
        Task<bool> ExistsByUserIdAsync(int userId);
        Task<UserBodyProfile> CreateAsync(UserBodyProfile profile);
        Task UpdateAsync(UserBodyProfile profile);
    }
}
