using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MV.ApplicationLayer.ServiceInterfaces;
using MV.DomainLayer.DTOs.Admin.Request;
using MV.DomainLayer.DTOs.Common;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

namespace MV.PresentationLayer.Controllers
{
    [Route("api/admin/categories")]
    [ApiController]
    [Authorize]
    public class AdminCategoryController : ControllerBase
    {
        private readonly IAdminProductService _adminProductService;

        public AdminCategoryController(IAdminProductService adminProductService)
        {
            _adminProductService = adminProductService;
        }

        /// <summary>
        /// Create a new category (Admin only)
        /// </summary>
        [HttpPost]
        [SwaggerOperation(Summary = "Create a new category (Admin only)")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryRequest request)
        {
            if (!IsAdmin())
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.ErrorResponse("Access denied. Admin role required."));

            var result = await _adminProductService.CreateCategoryAsync(request);
            if (!result.Success)
                return BadRequest(result);

            return StatusCode(StatusCodes.Status201Created, result);
        }

        /// <summary>
        /// Update a category (Admin only)
        /// </summary>
        [HttpPut("{categoryId}")]
        [SwaggerOperation(Summary = "Update a category (Admin only)")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateCategory(int categoryId, [FromBody] UpdateCategoryRequest request)
        {
            if (!IsAdmin())
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.ErrorResponse("Access denied. Admin role required."));

            var result = await _adminProductService.UpdateCategoryAsync(categoryId, request);
            if (!result.Success)
            {
                if (result.Message?.Contains("not found") == true)
                    return NotFound(result);
                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Delete a category (Admin only)
        /// </summary>
        [HttpDelete("{categoryId}")]
        [SwaggerOperation(Summary = "Delete a category (Admin only)")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteCategory(int categoryId)
        {
            if (!IsAdmin())
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.ErrorResponse("Access denied. Admin role required."));

            var result = await _adminProductService.DeleteCategoryAsync(categoryId);
            if (!result.Success)
            {
                if (result.Message?.Contains("not found") == true)
                    return NotFound(result);
                return BadRequest(result);
            }

            return Ok(result);
        }

        #region Helpers

        private bool IsAdmin()
        {
            var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;
            return int.TryParse(roleClaim, out var role) && role == 1;
        }

        #endregion
    }
}
