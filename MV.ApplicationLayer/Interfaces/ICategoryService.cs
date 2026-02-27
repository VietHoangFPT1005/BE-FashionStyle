using MV.DomainLayer.DTOs.Category.Response;
using MV.DomainLayer.DTOs.Common;

namespace MV.ApplicationLayer.ServiceInterfaces
{
    public interface ICategoryService
    {
        Task<ApiResponse<List<CategoryResponse>>> GetCategoriesAsync();
    }
}
