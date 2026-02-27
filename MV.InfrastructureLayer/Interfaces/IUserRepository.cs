using MV.DomainLayer.Entities;

namespace MV.InfrastructureLayer.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(int id);
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetByPhoneAsync(string phone);
        Task<User?> GetByUsernameAsync(string username);
        Task<User?> GetByEmailOrPhoneAsync(string emailOrPhone);
        Task<bool> ExistsByEmailAsync(string email);
        Task<bool> ExistsByUsernameAsync(string username);
        Task<bool> ExistsByPhoneAsync(string phone);
        Task<User> CreateAsync(User user);
        Task UpdateAsync(User user);
        Task<(List<User> Items, int TotalCount)> GetUsersPagedAsync(
            int page, int pageSize, int? role, bool? isActive, string? search);
    }
}
