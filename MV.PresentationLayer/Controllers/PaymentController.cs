using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MV.ApplicationLayer.ServiceInterfaces;
using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.DTOs.Payment.Request;
using MV.InfrastructureLayer.Interfaces;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;
using System.Text.Json;

namespace MV.PresentationLayer.Controllers
{
    [Route("api/Payment")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly IOrderRepository _orderRepo;
        private readonly IConfiguration _configuration;

        public PaymentController(IPaymentService paymentService, IOrderRepository orderRepo, IConfiguration configuration)
        {
            _paymentService = paymentService;
            _orderRepo = orderRepo;
            _configuration = configuration;
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
        [HttpPost("sepay-webhook")]
        [SwaggerOperation(Summary = "SePay Webhook - Nhận thông báo giao dịch từ SePay")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> SePayCallback()
        {
            // Verify SePay API key from Authorization header
            var webhookApiKey = _configuration["SePay:WebhookSecret"];
            if (!string.IsNullOrEmpty(webhookApiKey))
            {
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                var providedKey = authHeader?.StartsWith("Apikey ") == true
                    ? authHeader.Substring("Apikey ".Length).Trim()
                    : null;

                if (providedKey != webhookApiKey)
                    return Unauthorized(ApiResponse.ErrorResponse("Invalid webhook API key."));
            }

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
        [HttpGet("{orderId}/status")]
        [Authorize]
        [SwaggerOperation(Summary = "Lấy trạng thái thanh toán đơn hàng")]
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

        /// <summary>
        /// Admin/Staff manually verify a SEPAY payment (fallback when webhook fails)
        /// </summary>
        [HttpPut("{orderId}/verify")]
        [Authorize]
        [SwaggerOperation(Summary = "Admin xác nhận thanh toán thủ công (khi webhook SePay không hoạt động)")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> VerifyPaymentManually(int orderId)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized(ApiResponse.ErrorResponse("Invalid token."));

            var role = GetCurrentUserRole();
            if (role != 1 && role != 2)
                return StatusCode(403, ApiResponse.ErrorResponse("Only Admin or Staff can verify payments."));

            var result = await _paymentService.VerifyPaymentManuallyAsync(orderId, userId);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        // ==================== SEPAY UI CHECKOUT ====================

        [HttpGet("{orderId}/poll-status")]
        [AllowAnonymous]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> PollPaymentStatus(int orderId)
        {
            var order = await _orderRepo.GetByIdWithDetailsAsync(orderId);
            if (order == null || order.Payment == null)
                return NotFound();

            var payment = order.Payment;
            var status = payment.Status;

            if (status == "PENDING" && payment.PaymentMethod == "SEPAY"
                && payment.ExpiredAt.HasValue && payment.ExpiredAt.Value < DateTime.Now)
            {
                status = "EXPIRED";
            }

            var remainingSeconds = 0;
            if (payment.ExpiredAt.HasValue && payment.Status == "PENDING")
                remainingSeconds = Math.Max(0, (int)(payment.ExpiredAt.Value - DateTime.Now).TotalSeconds);

            return Ok(new
            {
                status,
                isPaid = payment.Status == "COMPLETED",
                remainingSeconds
            });
        }

        [HttpGet("{orderId}/checkout")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Redirect to SePay Checkout UI")]
        public async Task<IActionResult> RedirectToSepayCheckout(
            int orderId,
            [FromQuery] string? successUrl,
            [FromQuery] string? errorUrl,
            [FromQuery] string? cancelUrl)
        {
            var order = await _orderRepo.GetByIdWithDetailsAsync(orderId);
            if (order == null) return NotFound("Đơn hàng không tồn tại");

            var payment = order.Payment;
            if (payment == null) return NotFound("Chưa tạo record thanh toán");

            if (payment.PaymentMethod != "SEPAY")
                return BadRequest("Not a SEPAY payment");

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var backendSuccessUrl = $"{baseUrl}/api/Payment/callback/success?orderCode={order.OrderCode}";
            if (!string.IsNullOrEmpty(successUrl))
                backendSuccessUrl += $"&redirectUrl={Uri.EscapeDataString(successUrl)}";

            if (payment.Status == "COMPLETED")
                return Redirect(backendSuccessUrl);

            var finalCancelUrl = cancelUrl ?? "/";

            var sepayConfig = _configuration.GetSection("SePay");
            var bankName = sepayConfig["BankName"] ?? string.Empty;
            var accountNumber = sepayConfig["AccountNumber"] ?? string.Empty;
            var accountName = sepayConfig["AccountName"] ?? string.Empty;

            if (string.IsNullOrEmpty(payment.PaymentData))
            {
                await _paymentService.CreateSePayPaymentAsync(order.UserId, orderId);
                order = await _orderRepo.GetByIdWithDetailsAsync(orderId);
                payment = order!.Payment;
            }

            string qrCodeUrl = "";
            if (!string.IsNullOrEmpty(payment!.PaymentData))
            {
                try
                {
                    var data = JsonDocument.Parse(payment.PaymentData);
                    qrCodeUrl = data.RootElement.GetProperty("qrCodeUrl").GetString() ?? "";
                } catch {}
            }

            var expiredAt = payment.ExpiredAt
                ?? (payment.CreatedAt ?? DateTime.Now).AddMinutes(int.Parse(sepayConfig["PaymentExpiryMinutes"] ?? "10"));
            var remainingSeconds = Math.Max(0, (int)(expiredAt - DateTime.Now).TotalSeconds);

            if (remainingSeconds <= 0 || payment.Status == "EXPIRED" || order.Status == "CANCELLED")
            {
                return Content($@"
                    <html><head><meta http-equiv='refresh' content='3;url={finalCancelUrl}'></head>
                    <body style='font-family: Arial; text-align: center; padding: 50px;'>
                        <h2 style='color: red;'>Đơn hàng đã hết hạn hoặc bị huỷ!</h2>
                    </body></html>", "text/html");
            }

            var minutes = remainingSeconds / 60;
            var seconds = remainingSeconds % 60;

            var html = $@"
<!DOCTYPE html>
<html lang='vi'>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Thanh toán đơn hàng {order.OrderCode}</title>
    <link href='https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap' rel='stylesheet'>
    <style>
        body {{ font-family: 'Inter', sans-serif; background: #f5f7fa; margin: 0; display: flex; justify-content: center; align-items: center; min-height: 100vh; }}
        .card {{ background: white; border-radius: 20px; box-shadow: 0 10px 30px rgba(0,0,0,0.1); padding: 40px 30px; max-width: 400px; width: 100%; text-align: center; position: relative; }}
        .amount {{ font-size: 32px; color: #3498db; font-weight: 700; margin: 0 0 20px 0; }}
        .qr-image {{ max-width: 100%; border-radius: 8px; border: 1px solid #e2e8f0; }}
        .info-box {{ text-align: left; background: #f8fafc; padding: 15px; border-radius: 8px; margin-top: 20px; }}
        .timer {{ font-size: 18px; color: red; font-weight: bold; margin-top: 15px; }}
        .success-overlay {{ position: absolute; top:0; left:0; right:0; bottom:0; background: white; display: none; flex-direction: column; justify-content: center; align-items: center; border-radius: 20px; z-index: 10; }}
    </style>
</head>
<body>
    <div class='card'>
        <h2>Thanh Toán Đơn Hàng</h2>
        <div class='amount'>{payment.Amount:N0} VNĐ</div>
        <img class='qr-image' src='{qrCodeUrl}' />
        <div class='info-box'>
            <p><strong>Ngân hàng:</strong> {bankName}</p>
            <p><strong>Số TK:</strong> {accountNumber} ({accountName})</p>
            <p><strong>Nội dung CK:</strong> <span style='color: red; font-weight: bold'>{order.OrderCode}</span></p>
        </div>
        <div class='timer'>Thời gian còn lại: <span id='timer'>{minutes:D2}:{seconds:D2}</span></div>
        <div class='success-overlay' id='successOverlay'>
            <h1 style='color: green;'>✓ Thành Công!</h1>
            <p>Đang quay lại ứng dụng...</p>
        </div>
    </div>
    <script>
        let s = {remainingSeconds};
        const t = document.getElementById('timer');
        setInterval(() => {{
            s--;
            if (s<=0) window.location.href = '{finalCancelUrl}';
            else t.innerText = Math.floor(s/60).toString().padStart(2,'0') + ':' + (s%60).toString().padStart(2,'0');
        }}, 1000);
        setInterval(() => {{
            fetch('/api/Payment/{orderId}/poll-status')
                .then(r => r.json())
                .then(d => {{
                    if (d.status === 'COMPLETED' || d.isPaid) {{
                        document.getElementById('successOverlay').style.display = 'flex';
                        setTimeout(() => window.location.href = '{backendSuccessUrl}', 2000);
                    }}
                    if (d.status === 'EXPIRED') {{
                        window.location.href = '{finalCancelUrl}';
                    }}
                }});
        }}, 3000);
    </script>
</body>
</html>";
            return Content(html, "text/html");
        }

        [HttpGet("callback/success")]
        [AllowAnonymous]
        public async Task<IActionResult> SepaySuccessCallback([FromQuery] string orderCode, [FromQuery] string? redirectUrl)
        {
            // Process the payment success callback
            var result = await _paymentService.ProcessSuccessCallbackAsync(orderCode);

            if (!string.IsNullOrEmpty(redirectUrl))
            {
                var status = result.Success ? "success" : "error";
                return Redirect($"{redirectUrl}?status={status}&orderCode={orderCode}");
            }

            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }

        private int GetCurrentUserRole()
        {
            var roleClaim = User.FindFirst("role")?.Value
                ?? User.FindFirst(ClaimTypes.Role)?.Value;
            return int.TryParse(roleClaim, out var role) ? role : 0;
        }
    }
}
