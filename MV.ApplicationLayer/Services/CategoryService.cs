using MV.ApplicationLayer.ServiceInterfaces;
using MV.DomainLayer.DTOs.Category.Response;
using MV.DomainLayer.DTOs.Common;
using MV.InfrastructureLayer.Interfaces;

namespace MV.ApplicationLayer.Services
{
    public class CategoryService : ICategoryService
    {
        private readonly ICategoryRepository _categoryRepository;

        public CategoryService(ICategoryRepository categoryRepository)
        {
            _categoryRepository = categoryRepository;
        }

        public async Task<ApiResponse<List<CategoryResponse>>> GetCategoriesAsync()
        {
            var categories = await _categoryRepository.GetAllActiveWithProductCountAsync();

            // Build hierarchical tree: only root categories (ParentId == null)
            var rootCategories = categories
                .Where(c => c.ParentId == null)
                .Select(c => MapToCategoryResponse(c, categories))
                .ToList();

            return ApiResponse<List<CategoryResponse>>.SuccessResponse(rootCategories);
        }

        private CategoryResponse MapToCategoryResponse(DomainLayer.Entities.Category category,
            List<DomainLayer.Entities.Category> allCategories)
        {
            var children = allCategories
                .Where(c => c.ParentId == category.Id)
                .Select(c => MapToCategoryResponse(c, allCategories))
                .ToList();

            // Count products: own products + all children's products
            var ownProductCount = category.Products
                .Count(p => p.IsActive == true && p.IsDeleted == false);
            var childrenProductCount = children.Sum(c => c.ProductCount);

            return new CategoryResponse
            {
                CategoryId = category.Id,
                Name = category.Name,
                Slug = category.Slug,
                ImageUrl = category.ImageUrl,
                ParentId = category.ParentId,
                ProductCount = ownProductCount + childrenProductCount,
                Children = children.Count > 0 ? children : null
            };
        }
    }
}
