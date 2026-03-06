using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MV.ApplicationLayer.ServiceInterfaces;
using MV.DomainLayer.DTOs.Admin.Request;
using MV.DomainLayer.DTOs.Common;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

namespace MV.PresentationLayer.Controllers
{
    [Route("api/admin/vouchers")]
    [ApiController]
    [Authorize]
    public class AdminVoucherController : ControllerBase
    {
        private readonly IAdminProductService _adminProductService;

        public AdminVoucherController(IAdminProductService adminProductService)
        {
            _adminProductService = adminProductService;
        }

        /// <summary>
        /// Get all vouchers (Admin/Staff)
        /// </summary>
        [HttpGet]
        [SwaggerOperation(Summary = "Get all vouchers (Admin/Staff)")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetVouchers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] bool? isActive = null)
        {
            if (!IsAdminOrStaff())
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.ErrorResponse("Access denied. Admin or Staff role required."));

            var result = await _adminProductService.GetVouchersAsync(page, pageSize, isActive);
            return Ok(result);
        }

        /// <summary>
        /// Create a new voucher (Admin only)
        /// </summary>
        [HttpPost]
        [SwaggerOperation(Summary = "Create a new voucher (Admin only)")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateVoucher([FromBody] CreateVoucherRequest request)
        {
            if (!IsAdmin())
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.ErrorResponse("Access denied. Admin role required."));

            var result = await _adminProductService.CreateVoucherAsync(request);
            if (!result.Success)
                return BadRequest(result);

            return StatusCode(StatusCodes.Status201Created, result);
        }

        /// <summary>
        /// Update a voucher (Admin only)
        /// </summary>
        [HttpPut("{voucherId}")]
        [SwaggerOperation(Summary = "Update a voucher (Admin only)")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateVoucher(int voucherId, [FromBody] UpdateVoucherRequest request)
        {
            if (!IsAdmin())
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.ErrorResponse("Access denied. Admin role required."));

            var result = await _adminProductService.UpdateVoucherAsync(voucherId, request);
            if (!result.Success)
            {
                if (result.Message?.Contains("not found") == true)
                    return NotFound(result);
                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Delete a voucher (Admin only)
        /// </summary>
        [HttpDelete("{voucherId}")]
        [SwaggerOperation(Summary = "Delete a voucher (Admin only)")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteVoucher(int voucherId)
        {
            if (!IsAdmin())
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.ErrorResponse("Access denied. Admin role required."));

            var result = await _adminProductService.DeleteVoucherAsync(voucherId);
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

        private bool IsAdminOrStaff()
        {
            var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;
            return int.TryParse(roleClaim, out var role) && (role == 1 || role == 2);
        }

        #endregion
    }
}
