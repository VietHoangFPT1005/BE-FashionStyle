using MV.DomainLayer.Entities;

namespace MV.InfrastructureLayer.Interfaces
{
    public interface ISizeGuideRepository
    {
        Task<List<SizeGuide>> GetByProductIdAsync(int productId);
        Task<bool> ExistsByProductIdAsync(int productId);
    }
}
