using MV.DomainLayer.Entities;

namespace MV.InfrastructureLayer.Interfaces
{
    public interface IWishlistRepository
    {
        Task<List<Wishlist>> GetByUserIdAsync(int userId);
        Task<bool> ExistsAsync(int userId, int productId);
        Task<Wishlist> CreateAsync(Wishlist wishlist);
        Task<bool> DeleteAsync(int userId, int productId);
    }
}
