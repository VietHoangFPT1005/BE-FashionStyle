using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MV.ApplicationLayer.ServiceInterfaces;
using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.DTOs.Refund.Request;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

namespace MV.PresentationLayer.Controllers
{
    [Route("api")]
    [ApiController]
    [Authorize]
    public class RefundController : ControllerBase
    {
        private readonly IRefundService _refundService;

        public RefundController(IRefundService refundService)
        {
            _refundService = refundService;
        }

        #region Customer APIs

        /// <summary>
        /// Request a refund for a delivered order (within 7 days)
        /// </summary>
        [HttpPost("orders/{orderId}/refund")]
        [SwaggerOperation(Summary = "Request a refund for a delivered order")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RequestRefund(int orderId, [FromBody] CreateRefundRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized(ApiResponse.ErrorResponse("Invalid token."));

            var result = await _refundService.RequestRefundAsync(userId, orderId, request);
            if (!result.Success)
                return BadRequest(result);

            return StatusCode(StatusCodes.Status201Created, result);
        }

        /// <summary>
        /// Get refund status for an order
        /// </summary>
        [HttpGet("orders/{orderId}/refund")]
        [SwaggerOperation(Summary = "Get refund status for an order")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetRefundByOrder(int orderId)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized(ApiResponse.ErrorResponse("Invalid token."));

            var result = await _refundService.GetRefundByOrderAsync(userId, orderId);
            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }

        #endregion

        #region Admin APIs

        /// <summary>
        /// Get all refund requests (Admin/Staff)
        /// </summary>
        [HttpGet("admin/refunds")]
        [SwaggerOperation(Summary = "Get all refund requests (Admin/Staff)")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAllRefunds(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? status = null)
        {
            if (!IsAdminOrStaff())
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.ErrorResponse("Access denied. Admin or Staff role required."));

            var result = await _refundService.GetAllRefundsAsync(page, pageSize, status);
            return Ok(result);
        }

        /// <summary>
        /// Approve a refund request (Admin/Staff)
        /// </summary>
        [HttpPut("admin/refunds/{refundId}/approve")]
        [SwaggerOperation(Summary = "Approve a refund request (Admin/Staff)")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> ApproveRefund(int refundId, [FromBody] ProcessRefundRequest request)
        {
            if (!IsAdminOrStaff())
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.ErrorResponse("Access denied. Admin or Staff role required."));

            var adminId = GetCurrentUserId();
            var result = await _refundService.ApproveRefundAsync(adminId, refundId, request);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        /// <summary>
        /// Reject a refund request (Admin/Staff)
        /// </summary>
        [HttpPut("admin/refunds/{refundId}/reject")]
        [SwaggerOperation(Summary = "Reject a refund request (Admin/Staff)")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> RejectRefund(int refundId, [FromBody] ProcessRefundRequest request)
        {
            if (!IsAdminOrStaff())
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.ErrorResponse("Access denied. Admin or Staff role required."));

            var adminId = GetCurrentUserId();
            var result = await _refundService.RejectRefundAsync(adminId, refundId, request);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        #endregion

        #region Helpers

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }

        private int GetCurrentUserRole()
        {
            var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;
            return int.TryParse(roleClaim, out var role) ? role : 0;
        }

        private bool IsAdminOrStaff()
        {
            var role = GetCurrentUserRole();
            return role == 1 || role == 2;
        }

        #endregion
    }
}
