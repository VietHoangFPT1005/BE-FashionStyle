using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MV.ApplicationLayer.ServiceInterfaces;
using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.DTOs.Payment.Request;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

namespace MV.PresentationLayer.Controllers
{
    [Route("api/payments")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;

        public PaymentController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        /// <summary>
        /// Create SePay payment (generate QR code for bank transfer)
        /// </summary>
        [HttpPost("sepay/create")]
        [Authorize]
        [SwaggerOperation(Summary = "Create SePay payment")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CreateSePayPayment([FromBody] CreatePaymentRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized(ApiResponse.ErrorResponse("Invalid token."));

            var result = await _paymentService.CreateSePayPaymentAsync(userId, request.OrderId);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        /// <summary>
        /// SePay Webhook Callback (called by SePay when payment is completed)
        /// No authentication required - uses webhook verification
        /// </summary>
        [HttpPost("sepay/callback")]
        [SwaggerOperation(Summary = "SePay webhook callback (no auth)")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SePayCallback()
        {
            // Read raw body for checksum verification
            using var reader = new StreamReader(Request.Body);
            var jsonBody = await reader.ReadToEndAsync();

            var result = await _paymentService.HandleSePayCallbackAsync(jsonBody);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        /// <summary>
        /// Get payment status for an order
        /// </summary>
        [HttpGet("orders/{orderId}")]
        [Authorize]
        [SwaggerOperation(Summary = "Get payment status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetPaymentStatus(int orderId)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized(ApiResponse.ErrorResponse("Invalid token."));

            var result = await _paymentService.GetPaymentStatusAsync(userId, orderId);
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
