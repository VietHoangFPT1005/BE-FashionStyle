using Microsoft.Extensions.Options;
using MV.ApplicationLayer.ServiceInterfaces;
using MV.DomainLayer.Configuration;
using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.DTOs.Payment.Response;
using MV.DomainLayer.Entities;
using MV.InfrastructureLayer.Interfaces;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MV.ApplicationLayer.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IPaymentRepository _paymentRepository;
        private readonly IOrderRepository _orderRepository;
        private readonly INotificationRepository _notificationRepository;
        private readonly SePaySettings _sePaySettings;

        public PaymentService(
            IPaymentRepository paymentRepository,
            IOrderRepository orderRepository,
            INotificationRepository notificationRepository,
            IOptions<SePaySettings> sePaySettings)
        {
            _paymentRepository = paymentRepository;
            _orderRepository = orderRepository;
            _notificationRepository = notificationRepository;
            _sePaySettings = sePaySettings.Value;
        }

        public async Task<ApiResponse<SePayPaymentResponse>> CreateSePayPaymentAsync(int userId, int orderId)
        {
            // Verify order belongs to user
            var order = await _orderRepository.GetByIdAndUserIdAsync(orderId, userId);
            if (order == null)
                return ApiResponse<SePayPaymentResponse>.ErrorResponse("Order not found.");

            // Check payment status
            var payment = order.Payment;
            if (payment == null)
                return ApiResponse<SePayPaymentResponse>.ErrorResponse("Payment record not found.");

            if (payment.PaymentMethod != "SEPAY")
                return ApiResponse<SePayPaymentResponse>.ErrorResponse(
                    "This order does not use SePay payment method.");

            if (payment.Status == "COMPLETED")
                return ApiResponse<SePayPaymentResponse>.ErrorResponse(
                    "This order has already been paid.");

            if (order.Status == "CANCELLED")
                return ApiResponse<SePayPaymentResponse>.ErrorResponse(
                    "This order has been cancelled.");

            // Generate QR Code URL for bank transfer
            // SePay QR format: https://qr.sepay.vn/img?acc={accountNumber}&bank={bankCode}
            //   &amount={amount}&des={description}&template=compact
            var description = $"{order.OrderCode}";
            var qrCodeUrl = $"{_sePaySettings.QrBaseUrl}?acc={_sePaySettings.AccountNumber}" +
                            $"&bank={_sePaySettings.BankCode}" +
                            $"&amount={payment.Amount:0}" +
                            $"&des={Uri.EscapeDataString(description)}" +
                            $"&template=compact";

            var expiredAt = DateTime.Now.AddMinutes(_sePaySettings.PaymentExpiryMinutes);

            // Save payment data
            var paymentData = JsonSerializer.Serialize(new
            {
                qrCodeUrl,
                description,
                expiredAt,
                bankCode = _sePaySettings.BankCode,
                accountNumber = _sePaySettings.AccountNumber,
                accountName = _sePaySettings.AccountName
            });

            payment.PaymentData = paymentData;
            await _paymentRepository.UpdateAsync(payment);

            var response = new SePayPaymentResponse
            {
                QrCodeUrl = qrCodeUrl,
                Amount = payment.Amount,
                OrderCode = order.OrderCode,
                AccountNumber = _sePaySettings.AccountNumber,
                AccountName = _sePaySettings.AccountName,
                BankName = _sePaySettings.BankName,
                Description = description,
                ExpiredAt = expiredAt
            };

            return ApiResponse<SePayPaymentResponse>.SuccessResponse(response);
        }

        public async Task<ApiResponse<object>> HandleSePayCallbackAsync(string jsonBody)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonBody);
                var root = doc.RootElement;

                // Extract fields from SePay callback
                var content = root.TryGetProperty("content", out var contentProp)
                    ? contentProp.GetString() : null;
                var transferAmount = root.TryGetProperty("transferAmount", out var amountProp)
                    ? amountProp.GetDecimal() : 0;
                var gateway = root.TryGetProperty("gateway", out var gatewayProp)
                    ? gatewayProp.GetString() : null;
                var transactionDate = root.TryGetProperty("transactionDate", out var dateProp)
                    ? dateProp.GetString() : null;
                var referenceCode = root.TryGetProperty("referenceNumber", out var refProp)
                    ? refProp.GetString() : null;

                // Extract order code from content/description
                // SePay sends the transfer description which should contain our order code
                var orderCode = ExtractOrderCode(content);

                if (string.IsNullOrEmpty(orderCode))
                    return ApiResponse<object>.ErrorResponse("Cannot extract order code from callback.");

                // Find payment by order code
                var payment = await _paymentRepository.GetByOrderCodeAsync(orderCode);
                if (payment == null)
                    return ApiResponse<object>.ErrorResponse("Payment not found.");

                // Verify amount matches
                if (transferAmount > 0 && transferAmount != payment.Amount)
                {
                    // Amount mismatch - log but still process if close enough (rounding)
                    if (Math.Abs(transferAmount - payment.Amount) > 1)
                        return ApiResponse<object>.ErrorResponse("Payment amount mismatch.");
                }

                // Update payment status
                payment.Status = "COMPLETED";
                payment.TransactionId = referenceCode ?? $"SP-{DateTime.Now:yyyyMMddHHmmss}";
                payment.PaidAt = DateTime.Now;
                payment.PaymentData = jsonBody; // Store full callback data

                await _paymentRepository.UpdateAsync(payment);

                // Create notification for customer
                await _notificationRepository.CreateAsync(new Notification
                {
                    UserId = payment.Order.UserId,
                    Type = "PAYMENT",
                    Title = "Payment successful",
                    Message = $"Payment for order {orderCode} has been confirmed. Amount: {payment.Amount:N0}₫",
                    Data = $"{{\"orderId\":{payment.OrderId}}}",
                    IsRead = false,
                    CreatedAt = DateTime.Now
                });

                return ApiResponse<object>.SuccessResponse("Payment callback processed successfully.");
            }
            catch (JsonException)
            {
                return ApiResponse<object>.ErrorResponse("Invalid callback data format.");
            }
        }

        public async Task<ApiResponse<PaymentStatusResponse>> GetPaymentStatusAsync(int userId, int orderId)
        {
            var order = await _orderRepository.GetByIdAndUserIdAsync(orderId, userId);
            if (order == null)
                return ApiResponse<PaymentStatusResponse>.ErrorResponse("Order not found.");

            var payment = order.Payment;
            if (payment == null)
                return ApiResponse<PaymentStatusResponse>.ErrorResponse("Payment record not found.");

            var response = new PaymentStatusResponse
            {
                OrderId = order.Id,
                OrderCode = order.OrderCode,
                PaymentMethod = payment.PaymentMethod,
                Amount = payment.Amount,
                Status = payment.Status,
                TransactionId = payment.TransactionId,
                PaidAt = payment.PaidAt,
                CreatedAt = payment.CreatedAt
            };

            return ApiResponse<PaymentStatusResponse>.SuccessResponse(response);
        }

        #region Helpers

        /// <summary>
        /// Extract order code (ORD-XXXXXXXX-XXXX) from transfer description
        /// </summary>
        private string? ExtractOrderCode(string? content)
        {
            if (string.IsNullOrEmpty(content))
                return null;

            // Look for pattern ORD-YYYYMMDD-XXXX
            var match = System.Text.RegularExpressions.Regex.Match(
                content, @"ORD-\d{8}-\d{4}");

            return match.Success ? match.Value : null;
        }

        /// <summary>
        /// Verify HMAC-SHA256 checksum from SePay webhook
        /// </summary>
        private bool VerifyChecksum(string data, string checksum)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_sePaySettings.WebhookSecret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            var computedChecksum = Convert.ToHexString(hash).ToLower();
            return computedChecksum == checksum.ToLower();
        }

        #endregion
    }
}
