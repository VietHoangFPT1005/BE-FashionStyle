using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MV.ApplicationLayer.ServiceInterfaces;
using MV.DomainLayer.DTOs.Admin.Request;
using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.DTOs.Notification.Request;
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
        private readonly IAdminService _adminService;
        private readonly INotificationService _notificationService;

        public AdminController(
            IUserService userService,
            IOrderService orderService,
            IAdminService adminService,
            INotificationService notificationService)
        {
            _userService = userService;
            _orderService = orderService;
            _adminService = adminService;
            _notificationService = notificationService;
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

        /// <summary>
        /// Change user role (Admin only)
        /// </summary>
        [HttpPut("users/{userId}/role")]
        [SwaggerOperation(Summary = "Change user role (Admin only)")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> ChangeUserRole(int userId, [FromBody] ChangeRoleRequest request)
        {
            if (!IsAdmin())
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.ErrorResponse("Access denied. Admin role required."));

            var adminId = GetCurrentUserId();
            var result = await _adminService.ChangeUserRoleAsync(adminId, userId, request);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        /// <summary>
        /// Activate/Deactivate user (Admin only)
        /// </summary>
        [HttpPut("users/{userId}/status")]
        [SwaggerOperation(Summary = "Activate/Deactivate user (Admin only)")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> ChangeUserStatus(int userId, [FromBody] ChangeStatusRequest request)
        {
            if (!IsAdmin())
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.ErrorResponse("Access denied. Admin role required."));

            var adminId = GetCurrentUserId();
            var result = await _adminService.ChangeUserStatusAsync(adminId, userId, request);
            if (!result.Success)
                return BadRequest(result);

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

        #region Notification Management (Admin)

        /// <summary>
        /// Broadcast notification to all customers (Admin only)
        /// </summary>
        [HttpPost("notifications/broadcast")]
        [SwaggerOperation(Summary = "Broadcast notification to all customers (Admin only)")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> BroadcastNotification([FromBody] BroadcastNotificationRequest request)
        {
            if (!IsAdmin())
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.ErrorResponse("Access denied. Admin role required."));

            var result = await _notificationService.BroadcastAsync(request);
            if (!result.Success)
                return BadRequest(result);

            return StatusCode(StatusCodes.Status201Created, result);
        }

        #endregion

        #region Dashboard (Admin)

        /// <summary>
        /// Get admin dashboard statistics
        /// </summary>
        [HttpGet("dashboard")]
        [SwaggerOperation(Summary = "Get admin dashboard statistics")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetDashboard()
        {
            if (!IsAdmin())
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.ErrorResponse("Access denied. Admin role required."));

            var result = await _adminService.GetDashboardAsync();
            return Ok(result);
        }

        #endregion

        #region Helpers

        private bool IsAdmin()
        {
            var currentRole = GetCurrentUserRole();
            return currentRole == 1;
        }

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

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }

        #endregion
    }
}
