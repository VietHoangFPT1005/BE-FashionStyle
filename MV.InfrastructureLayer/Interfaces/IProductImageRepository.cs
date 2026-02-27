using MV.DomainLayer.Entities;

namespace MV.InfrastructureLayer.Interfaces
{
    public interface IProductImageRepository
    {
        Task<List<ProductImage>> GetByProductIdAsync(int productId);
        Task<string?> GetPrimaryImageUrlAsync(int productId);
    }
}
