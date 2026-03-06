using Microsoft.AspNetCore.Mvc;
using MV.ApplicationLayer.ServiceInterfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace MV.PresentationLayer.Controllers
{
    [Route("api/categories")]
    [ApiController]
    public class CategoryController : ControllerBase
    {
        private readonly ICategoryService _categoryService;
        private readonly IProductService _productService;

        public CategoryController(ICategoryService categoryService, IProductService productService)
        {
            _categoryService = categoryService;
            _productService = productService;
        }

        /// <summary>
        /// Get all categories with hierarchical structure
        /// </summary>
        [HttpGet]
        [SwaggerOperation(Summary = "Get all categories (hierarchical tree)")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCategories()
        {
            var result = await _categoryService.GetCategoriesAsync();
            return Ok(result);
        }

        /// <summary>
        /// Get products by category (includes child categories)
        /// </summary>
        [HttpGet("{categoryId}/products")]
        [SwaggerOperation(Summary = "Get products by category")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetProductsByCategory(
            int categoryId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 12,
            [FromQuery] string? gender = null,
            [FromQuery] string? search = null,
            [FromQuery] string? tags = null,
            [FromQuery] decimal? minPrice = null,
            [FromQuery] decimal? maxPrice = null,
            [FromQuery] string sortBy = "createdAt",
            [FromQuery] string sortOrder = "desc",
            [FromQuery] bool? isFeatured = null)
        {
            var result = await _productService.GetProductsByCategoryAsync(
                categoryId, page, pageSize, gender, search,
                tags, minPrice, maxPrice, sortBy, sortOrder, isFeatured);

            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }
    }
}
