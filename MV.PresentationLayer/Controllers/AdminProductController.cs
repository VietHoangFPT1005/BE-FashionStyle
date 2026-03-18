using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MV.ApplicationLayer.ServiceInterfaces;
using MV.DomainLayer.DTOs.Admin.Request;
using MV.DomainLayer.DTOs.Common;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

namespace MV.PresentationLayer.Controllers
{
    [Route("api/admin/products")]
    [ApiController]
    [Authorize]
    public class AdminProductController : ControllerBase
    {
        private readonly IAdminProductService _adminProductService;
        private readonly IConfiguration _config;

        public AdminProductController(IAdminProductService adminProductService, IConfiguration config)
        {
            _adminProductService = adminProductService;
            _config = config;
        }

        #region Product CRUD

        /// <summary>
        /// Get all products (Admin/Staff) with search and filter
        /// </summary>
        [HttpGet]
        [SwaggerOperation(Summary = "Get all products (Admin/Staff)")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetProducts(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null,
            [FromQuery] int? categoryId = null,
            [FromQuery] bool? isActive = null)
        {
            if (!IsAdminOrStaff())
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.ErrorResponse("Access denied. Admin or Staff role required."));

            var result = await _adminProductService.GetProductsAsync(page, pageSize, search, categoryId, isActive);
            return Ok(result);
        }

        /// <summary>
        /// Get product detail (Admin/Staff)
        /// </summary>
        [HttpGet("{productId}")]
        [SwaggerOperation(Summary = "Get product detail (Admin/Staff)")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetProductDetail(int productId)
        {
            if (!IsAdminOrStaff())
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.ErrorResponse("Access denied. Admin or Staff role required."));

            var result = await _adminProductService.GetProductDetailAsync(productId);
            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }

        /// <summary>
        /// Create a new product (Admin/Staff)
        /// </summary>
        [HttpPost]
        [SwaggerOperation(Summary = "Create a new product (Admin/Staff)")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request)
        {
            if (!IsAdminOrStaff())
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.ErrorResponse("Access denied. Admin or Staff role required."));

            var result = await _adminProductService.CreateProductAsync(request);
            if (!result.Success)
                return BadRequest(result);

            return StatusCode(StatusCodes.Status201Created, result);
        }

        /// <summary>
        /// Update a product (Admin/Staff)
        /// </summary>
        [HttpPut("{productId}")]
        [SwaggerOperation(Summary = "Update a product (Admin/Staff)")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateProduct(int productId, [FromBody] UpdateProductRequest request)
        {
            if (!IsAdminOrStaff())
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.ErrorResponse("Access denied. Admin or Staff role required."));

            var result = await _adminProductService.UpdateProductAsync(productId, request);
            if (!result.Success)
            {
                if (result.Message?.Contains("not found") == true)
                    return NotFound(result);
                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Delete (soft) a product (Admin only)
        /// </summary>
        [HttpDelete("{productId}")]
        [SwaggerOperation(Summary = "Delete a product (Admin only)")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteProduct(int productId)
        {
            if (!IsAdmin())
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.ErrorResponse("Access denied. Admin role required."));

            var result = await _adminProductService.DeleteProductAsync(productId);
            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }

        #endregion

        #region Variant CRUD

        /// <summary>
        /// Add variant to product (Admin/Staff)
        /// </summary>
        [HttpPost("{productId}/variants")]
        [SwaggerOperation(Summary = "Add variant to product (Admin/Staff)")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateVariant(int productId, [FromBody] CreateVariantRequest request)
        {
            if (!IsAdminOrStaff())
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.ErrorResponse("Access denied. Admin or Staff role required."));

            var result = await _adminProductService.CreateVariantAsync(productId, request);
            if (!result.Success)
                return BadRequest(result);

            return StatusCode(StatusCodes.Status201Created, result);
        }

        /// <summary>
        /// Update a variant (Admin/Staff)
        /// </summary>
        [HttpPut("variants/{variantId}")]
        [SwaggerOperation(Summary = "Update a variant (Admin/Staff)")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateVariant(int variantId, [FromBody] UpdateVariantRequest request)
        {
            if (!IsAdminOrStaff())
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.ErrorResponse("Access denied. Admin or Staff role required."));

            var result = await _adminProductService.UpdateVariantAsync(variantId, request);
            if (!result.Success)
            {
                if (result.Message?.Contains("not found") == true)
                    return NotFound(result);
                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Delete a variant (Admin only)
        /// </summary>
        [HttpDelete("variants/{variantId}")]
        [SwaggerOperation(Summary = "Delete a variant (Admin only)")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteVariant(int variantId)
        {
            if (!IsAdmin())
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.ErrorResponse("Access denied. Admin role required."));

            var result = await _adminProductService.DeleteVariantAsync(variantId);
            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }

        #endregion

        #region Image CRUD

        /// <summary>
        /// Upload product image to Cloudinary and return URL (Admin/Staff)
        /// </summary>
        [HttpPost("upload-image")]
        [Consumes("multipart/form-data")]
        [SwaggerOperation(Summary = "Upload product image to Cloudinary (Admin/Staff)")]
        public async Task<IActionResult> UploadProductImage([FromForm] UploadImageRequest request)
        {
            if (!IsAdminOrStaff())
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.ErrorResponse("Access denied. Admin or Staff role required."));

            var file = request.File;
            if (file == null || file.Length == 0)
                return BadRequest(ApiResponse.ErrorResponse("Không có file được gửi lên."));

            var cloudName = _config["CloudinarySettings:CloudName"];
            var apiKey    = _config["CloudinarySettings:ApiKey"];
            var apiSecret = _config["CloudinarySettings:ApiSecret"];

            var account    = new Account(cloudName, apiKey, apiSecret);
            var cloudinary = new Cloudinary(account);

            using var stream = file.OpenReadStream();
            var uploadParams = new ImageUploadParams
            {
                File        = new FileDescription(file.FileName, stream),
                Folder      = "product-images",
                UseFilename = false,
            };

            var result = await cloudinary.UploadAsync(uploadParams);
            if (result.Error != null)
                return BadRequest(ApiResponse.ErrorResponse(result.Error.Message));

            var imageUrl = result.SecureUrl.ToString();
            return Ok(new ApiResponse<string> { Success = true, Data = imageUrl, Message = "Upload thành công." });
        }

        /// <summary>
        /// Add image to product (Admin/Staff)
        /// </summary>
        [HttpPost("{productId}/images")]
        [SwaggerOperation(Summary = "Add image to product (Admin/Staff)")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateProductImage(int productId, [FromBody] CreateProductImageRequest request)
        {
            if (!IsAdminOrStaff())
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.ErrorResponse("Access denied. Admin or Staff role required."));

            var result = await _adminProductService.CreateProductImageAsync(productId, request);
            if (!result.Success)
                return BadRequest(result);

            return StatusCode(StatusCodes.Status201Created, result);
        }

        /// <summary>
        /// Delete a product image (Admin only)
        /// </summary>
        [HttpDelete("images/{imageId}")]
        [SwaggerOperation(Summary = "Delete a product image (Admin only)")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteProductImage(int imageId)
        {
            if (!IsAdmin())
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.ErrorResponse("Access denied. Admin role required."));

            var result = await _adminProductService.DeleteProductImageAsync(imageId);
            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }

        #endregion

        #region Size Guide

        /// <summary>
        /// Create/update size guide for product (Admin/Staff)
        /// </summary>
        [HttpPut("{productId}/size-guide")]
        [SwaggerOperation(Summary = "Create/update size guide for product (Admin/Staff)")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpsertSizeGuide(int productId, [FromBody] CreateSizeGuideRequest request)
        {
            if (!IsAdminOrStaff())
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.ErrorResponse("Access denied. Admin or Staff role required."));

            var result = await _adminProductService.UpsertSizeGuideAsync(productId, request);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        #endregion

        #region Helpers

        private bool IsAdmin()
        {
            var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;
            return int.TryParse(roleClaim, out var role) && role == 1;
        }

        private bool IsAdminOrStaff()
        {
            var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;
            return int.TryParse(roleClaim, out var role) && (role == 1 || role == 2);
        }

        #endregion
    }
}
