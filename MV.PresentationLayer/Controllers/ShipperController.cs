using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MV.ApplicationLayer.ServiceInterfaces;
using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.DTOs.Shipper.Request;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

namespace MV.PresentationLayer.Controllers
{
    [Route("api/shipper")]
    [ApiController]
    [Authorize]
    public class ShipperController : ControllerBase
    {
        private readonly IShipperService _shipperService;

        public ShipperController(IShipperService shipperService)
        {
            _shipperService = shipperService;
        }

        /// <summary>
        /// Get shipper's assigned orders
        /// </summary>
        [HttpGet("orders")]
        [SwaggerOperation(Summary = "Get shipper's assigned orders")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetShipperOrders([FromQuery] string? status = null)
        {
            if (!IsShipper())
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.ErrorResponse("Access denied. Shipper role required."));

            var shipperId = GetCurrentUserId();
            var result = await _shipperService.GetShipperOrdersAsync(shipperId, status);
            return Ok(result);
        }

        /// <summary>
        /// Pickup order - start shipping (PROCESSING → SHIPPING). Requires tracking number.
        /// </summary>
        [HttpPut("orders/{orderId}/pickup")]
        [SwaggerOperation(Summary = "Pickup order - start shipping (requires tracking number)")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> PickupOrder(int orderId, [FromBody] PickupOrderRequest request)
        {
            if (!IsShipper())
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.ErrorResponse("Access denied. Shipper role required."));

            var shipperId = GetCurrentUserId();
            var result = await _shipperService.PickupOrderAsync(shipperId, orderId, request);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        /// <summary>
        /// Deliver order - confirm delivery (SHIPPING → DELIVERED)
        /// </summary>
        [HttpPut("orders/{orderId}/deliver")]
        [SwaggerOperation(Summary = "Deliver order - confirm delivery")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeliverOrder(int orderId)
        {
            if (!IsShipper())
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.ErrorResponse("Access denied. Shipper role required."));

            var shipperId = GetCurrentUserId();
            var result = await _shipperService.DeliverOrderAsync(shipperId, orderId);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        /// <summary>
        /// Report delivery failure
        /// </summary>
        [HttpPut("orders/{orderId}/fail")]
        [SwaggerOperation(Summary = "Report delivery failure")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeliveryFailed(int orderId, [FromBody] DeliveryFailedRequest request)
        {
            if (!IsShipper())
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.ErrorResponse("Access denied. Shipper role required."));

            var shipperId = GetCurrentUserId();
            var result = await _shipperService.DeliveryFailedAsync(shipperId, orderId, request);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        /// <summary>
        /// Update shipper GPS location
        /// </summary>
        [HttpPost("location")]
        [SwaggerOperation(Summary = "Update shipper GPS location")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateLocation([FromBody] UpdateLocationRequest request)
        {
            if (!IsShipper())
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.ErrorResponse("Access denied. Shipper role required."));

            var shipperId = GetCurrentUserId();
            var result = await _shipperService.UpdateLocationAsync(shipperId, request);
            if (!result.Success)
                return BadRequest(result);

            return StatusCode(StatusCodes.Status201Created, result);
        }

        #region Helpers

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }

        private bool IsShipper()
        {
            var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;
            return int.TryParse(roleClaim, out var role) && role == 4;
        }

        #endregion
    }
}
