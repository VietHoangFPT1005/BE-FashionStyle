using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MV.ApplicationLayer.ServiceInterfaces;
using MV.DomainLayer.DTOs.Common;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

namespace MV.PresentationLayer.Controllers
{
    [Route("api/products")]
    [ApiController]
    public class ProductController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductController(IProductService productService)
        {
            _productService = productService;
        }

        /// <summary>
        /// Get products list with filtering, sorting, and pagination
        /// </summary>
        [HttpGet]
        [SwaggerOperation(Summary = "Get products list (filter/sort/paginate)")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetProducts(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 12,
            [FromQuery] int? categoryId = null,
            [FromQuery] string? gender = null,
            [FromQuery] string? search = null,
            [FromQuery] string? tags = null,
            [FromQuery] decimal? minPrice = null,
            [FromQuery] decimal? maxPrice = null,
            [FromQuery] string sortBy = "createdAt",
            [FromQuery] string sortOrder = "desc",
            [FromQuery] bool? isFeatured = null)
        {
            var result = await _productService.GetProductsAsync(
                page, pageSize, categoryId, gender, search,
                tags, minPrice, maxPrice, sortBy, sortOrder, isFeatured);

            return Ok(result);
        }

        /// <summary>
        /// Search products (autocomplete)
        /// </summary>
        [HttpGet("search")]
        [SwaggerOperation(Summary = "Search products (autocomplete)")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SearchProducts(
            [FromQuery] string q = "",
            [FromQuery] int limit = 10)
        {
            var result = await _productService.SearchProductsAsync(q, limit);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        /// <summary>
        /// Get product detail by ID (increments ViewCount)
        /// </summary>
        [HttpGet("{productId}")]
        [SwaggerOperation(Summary = "Get product detail")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetProductDetail(int productId)
        {
            var result = await _productService.GetProductDetailAsync(productId);
            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }

        /// <summary>
        /// Get product variants (sizes, colors, stock)
        /// </summary>
        [HttpGet("{productId}/variants")]
        [SwaggerOperation(Summary = "Get product variants")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetProductVariants(int productId)
        {
            var result = await _productService.GetProductVariantsAsync(productId);
            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }

        /// <summary>
        /// Get product images
        /// </summary>
        [HttpGet("{productId}/images")]
        [SwaggerOperation(Summary = "Get product images")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetProductImages(int productId)
        {
            var result = await _productService.GetProductImagesAsync(productId);
            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }

        /// <summary>
        /// Get size guide for a product (Big Size feature)
        /// </summary>
        [HttpGet("{productId}/size-guide")]
        [SwaggerOperation(Summary = "Get product size guide")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetSizeGuide(int productId)
        {
            var result = await _productService.GetSizeGuideAsync(productId);
            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }

        /// <summary>
        /// AI Fit-Check: Recommend size based on user body profile (Core Big Size feature)
        /// </summary>
        [HttpGet("{productId}/recommend-size")]
        [Authorize]
        [SwaggerOperation(Summary = "AI Recommend Size (requires authentication)")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RecommendSize(int productId)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized(ApiResponse.ErrorResponse("Invalid token."));

            var result = await _productService.RecommendSizeAsync(productId, userId);
            if (!result.Success)
            {
                if (result.Message != null && result.Message.Contains("body measurements"))
                    return BadRequest(result);
                return NotFound(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Get product reviews with pagination and rating filter
        /// </summary>
        [HttpGet("{productId}/reviews")]
        [SwaggerOperation(Summary = "Get product reviews")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetProductReviews(
            int productId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] int? rating = null,
            [FromQuery] string sortBy = "createdAt")
        {
            var result = await _productService.GetProductReviewsAsync(
                productId, page, pageSize, rating, sortBy);

            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }
    }
}
