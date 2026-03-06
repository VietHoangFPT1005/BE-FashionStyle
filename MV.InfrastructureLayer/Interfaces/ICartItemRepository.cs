using MV.DomainLayer.Entities;

namespace MV.InfrastructureLayer.Interfaces
{
    public interface ICartItemRepository
    {
        Task<List<CartItem>> GetByUserIdAsync(int userId);
        Task<CartItem?> GetByIdAndUserIdAsync(int cartItemId, int userId);
        Task<CartItem?> GetByUserIdAndVariantIdAsync(int userId, int productVariantId);
        Task<CartItem> CreateAsync(CartItem cartItem);
        Task UpdateAsync(CartItem cartItem);
        Task DeleteAsync(CartItem cartItem);
        Task DeleteAllByUserIdAsync(int userId);
    }
}
