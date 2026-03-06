using MV.DomainLayer.Entities;

namespace MV.InfrastructureLayer.Interfaces
{
    public interface IProductVariantRepository
    {
        Task<List<ProductVariant>> GetByProductIdAsync(int productId);
        Task<ProductVariant?> GetByIdAsync(int id);
        Task<bool> ExistsAndActiveAsync(int variantId);
    }
}
