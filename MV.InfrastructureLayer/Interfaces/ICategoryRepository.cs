using MV.DomainLayer.Entities;

namespace MV.InfrastructureLayer.Interfaces
{
    public interface ICategoryRepository
    {
        Task<List<Category>> GetAllActiveWithProductCountAsync();
        Task<Category?> GetByIdAsync(int id);
        Task<List<int>> GetChildCategoryIdsAsync(int parentId);
    }
}
