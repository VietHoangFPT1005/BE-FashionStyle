using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MV.ApplicationLayer.ServiceInterfaces;
using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.DTOs.Order.Request;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

namespace MV.PresentationLayer.Controllers
{
    [Route("api/admin")]
    [ApiController]
    [Authorize]
    public class AdminController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IOrderService _orderService;

        public AdminController(IUserService userService, IOrderService orderService)
        {
            _userService = userService;
            _orderService = orderService;
        }

        #region User Management

        /// <summary>
        /// Get paginated list of users (Admin only)
        /// </summary>
        [HttpGet("users")]
        [SwaggerOperation(Summary = "Get paginated list of users (Admin only)")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetUsers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] int? role = null,
            [FromQuery] bool? isActive = null,
            [FromQuery] string? search = null)
        {
            if (!IsAdminOrStaff())
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.ErrorResponse("Access denied. Admin role required."));

            var result = await _userService.GetUsersAsync(page, pageSize, role, isActive, search);
            return Ok(result);
        }

        #endregion

        #region Order Management (Admin/Staff)

        /// <summary>
        /// Get all orders (Admin/Staff)
        /// </summary>
        [HttpGet("orders")]
        [SwaggerOperation(Summary = "Get all orders (Admin/Staff)")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAllOrders(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? status = null,
            [FromQuery] string? search = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            if (!IsAdminOrStaff())
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.ErrorResponse("Access denied. Admin or Staff role required."));

            var result = await _orderService.GetAllOrdersAsync(
                page, pageSize, status, search, startDate, endDate);
            return Ok(result);
        }

        /// <summary>
        /// Get order detail (Admin/Staff)
        /// </summary>
        [HttpGet("orders/{orderId}")]
        [SwaggerOperation(Summary = "Get order detail (Admin/Staff)")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetOrderDetail(int orderId)
        {
            if (!IsAdminOrStaff())
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.ErrorResponse("Access denied. Admin or Staff role required."));

            var result = await _orderService.GetAdminOrderDetailAsync(orderId);
            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }

        /// <summary>
        /// Confirm order (PENDING → CONFIRMED)
        /// </summary>
        [HttpPut("orders/{orderId}/confirm")]
        [SwaggerOperation(Summary = "Confirm order (Admin/Staff)")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> ConfirmOrder(int orderId)
        {
            if (!IsAdminOrStaff())
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.ErrorResponse("Access denied. Admin or Staff role required."));

            var result = await _orderService.ConfirmOrderAsync(orderId);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        /// <summary>
        /// Assign shipper to order (CONFIRMED → PROCESSING)
        /// </summary>
        [HttpPut("orders/{orderId}/assign-shipper")]
        [SwaggerOperation(Summary = "Assign shipper to order (Admin/Staff)")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> AssignShipper(int orderId, [FromBody] AssignShipperRequest request)
        {
            if (!IsAdminOrStaff())
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.ErrorResponse("Access denied. Admin or Staff role required."));

            var result = await _orderService.AssignShipperAsync(orderId, request);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        /// <summary>
        /// Cancel order (Admin/Staff - any status except DELIVERED)
        /// </summary>
        [HttpPut("orders/{orderId}/cancel")]
        [SwaggerOperation(Summary = "Cancel order (Admin/Staff)")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> AdminCancelOrder(int orderId, [FromBody] CancelOrderRequest request)
        {
            if (!IsAdminOrStaff())
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.ErrorResponse("Access denied. Admin or Staff role required."));

            var result = await _orderService.AdminCancelOrderAsync(orderId, request);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        #endregion

        #region Helpers

        private bool IsAdminOrStaff()
        {
            var currentRole = GetCurrentUserRole();
            return currentRole == 1 || currentRole == 2; // Admin=1, Staff=2
        }

        private int GetCurrentUserRole()
        {
            var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;
            return int.TryParse(roleClaim, out var role) ? role : 0;
        }

        #endregion
    }
}
