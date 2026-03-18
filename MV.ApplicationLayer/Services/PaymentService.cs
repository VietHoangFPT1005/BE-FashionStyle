using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MV.ApplicationLayer.ServiceInterfaces;
using MV.DomainLayer.Configuration;
using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.DTOs.Payment.Response;
using MV.DomainLayer.Entities;
using MV.InfrastructureLayer.DBContext;
using MV.InfrastructureLayer.Interfaces;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MV.ApplicationLayer.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly FashionDbContext _context;
        private readonly IPaymentRepository _paymentRepository;
        private readonly IOrderRepository _orderRepository;
        private readonly ISepayTransactionRepository _sepayTransactionRepository;
        private readonly INotificationRepository _notificationRepository;
        private readonly SePaySettings _sePaySettings;
        private readonly ILogger<PaymentService> _logger;

        public PaymentService(
            FashionDbContext context,
            IPaymentRepository paymentRepository,
            IOrderRepository orderRepository,
            ISepayTransactionRepository sepayTransactionRepository,
            INotificationRepository notificationRepository,
            IOptions<SePaySettings> sePaySettings,
            ILogger<PaymentService> logger)
        {
            _context = context;
            _paymentRepository = paymentRepository;
            _orderRepository = orderRepository;
            _sepayTransactionRepository = sepayTransactionRepository;
            _notificationRepository = notificationRepository;
            _sePaySettings = sePaySettings.Value;
            _logger = logger;
        }

        public async Task<ApiResponse<SePayPaymentResponse>> CreateSePayPaymentAsync(int userId, int orderId)
        {
            var order = await _orderRepository.GetByIdAndUserIdAsync(orderId, userId);
            if (order == null)
                return ApiResponse<SePayPaymentResponse>.ErrorResponse("Order not found.");

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

            if (payment.Status == "EXPIRED")
                return ApiResponse<SePayPaymentResponse>.ErrorResponse(
                    "This payment has expired. Please create a new order.");

            var description = $"{order.OrderCode}";
            var qrCodeUrl = $"{_sePaySettings.QrBaseUrl}?acc={_sePaySettings.AccountNumber}" +
                            $"&bank={_sePaySettings.BankCode}" +
                            $"&amount={payment.Amount:0}" +
                            $"&des={Uri.EscapeDataString(description)}" +
                            $"&template=compact";

            var expiredAt = DateTime.Now.AddMinutes(_sePaySettings.PaymentExpiryMinutes);

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
            payment.ExpiredAt = expiredAt;
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

                // Extract SePay webhook fields
                var sepayId = root.TryGetProperty("id", out var idProp)
                    ? idProp.ToString() : null;
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
                var transferType = root.TryGetProperty("transferType", out var typeProp)
                    ? typeProp.GetString() : null;

                // Only process incoming transfers
                if (transferType != null && transferType != "in")
                {
                    _logger.LogInformation("Ignoring non-incoming transfer: {TransferType}", transferType);
                    return ApiResponse<object>.SuccessResponse("Ignored non-incoming transfer.");
                }

                if (!string.IsNullOrEmpty(sepayId))
                {
                    var exists = await _sepayTransactionRepository.ExistsBySepayIdAsync(sepayId);
                    if (exists)
                    {
                        _logger.LogInformation("Duplicate SePay transaction ignored: {SepayId}", sepayId);
                        return ApiResponse<object>.SuccessResponse("Duplicate transaction ignored.");
                    }
                }

                DateTime? parsedDate = null;
                if (!string.IsNullOrEmpty(transactionDate) && DateTime.TryParse(transactionDate, out var dt))
                    parsedDate = dt;
                
                var sepayTransaction = new SepayTransaction
                {
                    SepayId = sepayId,
                    Gateway = gateway,
                    TransactionDate = parsedDate,
                    AccountNumber = root.TryGetProperty("accountNumber", out var accProp) ? accProp.GetString() : null,
                    TransferType = transferType,
                    TransferAmount = transferAmount,
                    Accumulated = root.TryGetProperty("accumulated", out var accuProp) ? accuProp.GetDecimal() : (decimal?)null,
                    Code = root.TryGetProperty("code", out var codeProp) ? codeProp.GetString() : null,
                    Content = content,
                    ReferenceNumber = referenceCode,
                    Description = root.TryGetProperty("description", out var descProp) ? descProp.GetString() : null,
                    IsProcessed = false,
                    RawData = jsonBody,
                    CreatedAt = DateTime.Now
                };

                // Extract order code from content
                var orderCode = ExtractOrderCode(content);

                if (string.IsNullOrEmpty(orderCode))
                {
                    await _sepayTransactionRepository.CreateAsync(sepayTransaction);
                    _logger.LogWarning("Cannot extract order code from SePay callback. Content: {Content}", content);
                    return ApiResponse<object>.SuccessResponse("Transaction logged but no order code found.");
                }

                // Find payment by order code
                var payment = await _paymentRepository.GetByOrderCodeAsync(orderCode);
                if (payment == null)
                {
                    await _sepayTransactionRepository.CreateAsync(sepayTransaction);
                    _logger.LogWarning("Payment not found for order code: {OrderCode}", orderCode);
                    return ApiResponse<object>.SuccessResponse("Transaction logged but payment not found.");
                }

                sepayTransaction.OrderId = payment.OrderId;

                // Check if payment already processed (idempotent)
                if (payment.Status == "COMPLETED" || payment.Status == "FAILED" || payment.Status == "EXPIRED")
                {
                    sepayTransaction.IsProcessed = true;
                    await _sepayTransactionRepository.CreateAsync(sepayTransaction);
                    _logger.LogInformation("Payment already {Status} for order {OrderCode}", payment.Status, orderCode);
                    return ApiResponse<object>.SuccessResponse("Payment already processed.");
                }

                // Verify amount matches (allow 1₫ rounding tolerance)
                if (transferAmount > 0 && Math.Abs(transferAmount - payment.Amount) > 1)
                {
                    // IsProcessed = false: logged but NOT completed, needs manual admin review
                    sepayTransaction.IsProcessed = false;
                    await _sepayTransactionRepository.CreateAsync(sepayTransaction);
                    _logger.LogWarning("Amount mismatch for {OrderCode}: expected {Expected}, received {Received}",
                        orderCode, payment.Amount, transferAmount);
                    return ApiResponse<object>.ErrorResponse("Payment amount mismatch.");
                }

                // Process payment in transaction
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Update payment status
                    payment.Status = "COMPLETED";
                    payment.TransactionId = referenceCode ?? sepayId ?? $"SP-{DateTime.Now:yyyyMMddHHmmss}";
                    payment.PaidAt = DateTime.Now;
                    payment.PaymentData = jsonBody;
                    await _paymentRepository.UpdateAsync(payment);

                    // Auto-confirm order if PENDING
                    var order = payment.Order;
                    if (order != null && order.Status == "PENDING")
                    {
                        order.Status = "CONFIRMED";
                        order.ConfirmedAt = DateTime.Now;
                        await _orderRepository.UpdateAsync(order);
                        _logger.LogInformation("Order {OrderCode} auto-confirmed after payment", orderCode);
                    }

                    sepayTransaction.IsProcessed = true;
                    await _sepayTransactionRepository.CreateAsync(sepayTransaction);

                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error processing payment for order {OrderCode}", orderCode);
                    throw;
                }

                // Send notification (outside transaction)
                try
                {
                    await _notificationRepository.CreateAsync(new Notification
                    {
                        UserId = payment.Order.UserId,
                        Type = "PAYMENT",
                        Title = "Thanh toán thành công",
                        Message = $"Thanh toán đơn hàng {orderCode} đã được xác nhận. Số tiền: {payment.Amount:N0}₫",
                        Data = $"{{\"orderId\":{payment.OrderId}}}",
                        IsRead = false,
                        CreatedAt = DateTime.Now
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create notification for order {OrderCode}", orderCode);
                }

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

            var status = payment.Status;
            if (status == "PENDING" && payment.PaymentMethod == "SEPAY"
                && payment.ExpiredAt.HasValue && payment.ExpiredAt.Value < DateTime.Now)
            {
                status = "EXPIRED";
            }

            var remainingSeconds = 0;
            if (payment.ExpiredAt.HasValue && payment.Status == "PENDING")
                remainingSeconds = Math.Max(0, (int)(payment.ExpiredAt.Value - DateTime.Now).TotalSeconds);

            var response = new PaymentStatusResponse
            {
                OrderId = order.Id,
                OrderCode = order.OrderCode,
                PaymentMethod = payment.PaymentMethod,
                Amount = payment.Amount,
                Status = status,
                TransactionId = payment.TransactionId,
                PaidAt = payment.PaidAt,
                CreatedAt = payment.CreatedAt,
                ExpiredAt = payment.ExpiredAt,
                IsPaid = payment.Status == "COMPLETED",
                RemainingSeconds = remainingSeconds
            };

            return ApiResponse<PaymentStatusResponse>.SuccessResponse(response);
        }

        public async Task ExpireOverduePaymentsAsync()
        {
            var expiredPayments = await _paymentRepository.GetExpiredPendingSePayPaymentsAsync();

            if (!expiredPayments.Any())
                return;

            _logger.LogInformation("Found {Count} expired SEPAY payments to process.", expiredPayments.Count);

            foreach (var payment in expiredPayments)
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var order = payment.Order;
                    if (order == null) continue;

                    // 1. Mark payment as EXPIRED
                    payment.Status = "EXPIRED";
                    await _paymentRepository.UpdateAsync(payment);

                    // 2. Cancel the order
                    order.Status = "CANCELLED";
                    order.CancelReason = "Hết thời gian thanh toán SEPAY.";
                    order.CancelledAt = DateTime.Now;

                    // 3. Restore stock + decrease sold count
                    foreach (var oi in order.OrderItems)
                    {
                        if (oi.ProductVariantId.HasValue)
                        {
                            await _context.ProductVariants
                                .Where(v => v.Id == oi.ProductVariantId.Value)
                                .ExecuteUpdateAsync(s => s.SetProperty(
                                    v => v.StockQuantity, v => (v.StockQuantity ?? 0) + oi.Quantity));

                            var variant = await _context.ProductVariants
                                .FirstOrDefaultAsync(v => v.Id == oi.ProductVariantId.Value);
                            if (variant != null)
                            {
                                await _context.Products
                                    .Where(p => p.Id == variant.ProductId)
                                    .ExecuteUpdateAsync(s => s.SetProperty(
                                        p => p.SoldCount, p => Math.Max(0, (p.SoldCount ?? 0) - oi.Quantity)));
                            }
                        }
                    }

                    // 4. Restore voucher UsedCount if voucher was used
                    if (order.VoucherId.HasValue)
                    {
                        var voucher = await _context.Vouchers
                            .FirstOrDefaultAsync(v => v.Id == order.VoucherId.Value);
                        if (voucher != null)
                        {
                            voucher.UsedCount = Math.Max(0, (voucher.UsedCount ?? 0) - 1);
                            _context.Vouchers.Update(voucher);
                        }
                    }

                    _context.Orders.Update(order);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // 5. Send notification (outside transaction to avoid blocking)
                    try
                    {
                        await _notificationRepository.CreateAsync(new Notification
                        {
                            UserId = order.UserId,
                            Type = "PAYMENT",
                            Title = "Thanh toán hết hạn",
                            Message = $"Đơn hàng {order.OrderCode} đã bị hủy do hết thời gian thanh toán. Vui lòng đặt lại đơn hàng mới.",
                            Data = $"{{\"orderId\":{order.Id}}}",
                            IsRead = false,
                            CreatedAt = DateTime.Now
                        });
                    }
                    catch (Exception notifEx)
                    {
                        _logger.LogError(notifEx, "Failed to send expiry notification for Order {OrderId}", order.Id);
                    }

                    _logger.LogInformation(
                        "Expired payment {PaymentId} for Order {OrderCode}. Stock restored, voucher restored, order cancelled.",
                        payment.Id, order.OrderCode);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error expiring payment {PaymentId}", payment.Id);
                }
            }
        }

        public async Task<ApiResponse<PaymentStatusResponse>> VerifyPaymentManuallyAsync(int orderId, int adminUserId)
        {
            var order = await _orderRepository.GetByIdWithDetailsAsync(orderId);
            if (order == null)
                return ApiResponse<PaymentStatusResponse>.ErrorResponse("Order not found.");

            var payment = order.Payment;
            if (payment == null)
                return ApiResponse<PaymentStatusResponse>.ErrorResponse("Payment record not found.");

            if (payment.PaymentMethod != "SEPAY")
                return ApiResponse<PaymentStatusResponse>.ErrorResponse(
                    "Only SEPAY payments can be manually verified.");

            if (payment.Status == "COMPLETED")
                return ApiResponse<PaymentStatusResponse>.ErrorResponse(
                    "This payment has already been completed.");

            if (payment.Status != "PENDING")
                return ApiResponse<PaymentStatusResponse>.ErrorResponse(
                    $"Cannot verify payment with status '{payment.Status}'. Only PENDING payments can be verified.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                payment.Status = "COMPLETED";
                payment.PaidAt = DateTime.Now;
                payment.TransactionId = $"ADMIN-VERIFY-{adminUserId}-{DateTime.Now:yyyyMMddHHmmss}";
                await _paymentRepository.UpdateAsync(payment);

                // Auto-confirm order if PENDING
                if (order.Status == "PENDING")
                {
                    order.Status = "CONFIRMED";
                    order.ConfirmedAt = DateTime.Now;
                    await _orderRepository.UpdateAsync(order);
                }

                await transaction.CommitAsync();

                // Notification
                await _notificationRepository.CreateAsync(new Notification
                {
                    UserId = order.UserId,
                    Type = "PAYMENT",
                    Title = "Thanh toán đã được xác nhận",
                    Message = $"Thanh toán đơn hàng {order.OrderCode} đã được xác nhận bởi quản trị viên.",
                    Data = $"{{\"orderId\":{order.Id}}}",
                    IsRead = false,
                    CreatedAt = DateTime.Now
                });

                _logger.LogInformation("AUDIT: Admin {AdminId} manually verified payment for Order {OrderId}", adminUserId, orderId);

                return ApiResponse<PaymentStatusResponse>.SuccessResponse(new PaymentStatusResponse
                {
                    OrderId = order.Id,
                    OrderCode = order.OrderCode,
                    PaymentMethod = payment.PaymentMethod,
                    Amount = payment.Amount,
                    Status = "COMPLETED",
                    TransactionId = payment.TransactionId,
                    PaidAt = payment.PaidAt,
                    CreatedAt = payment.CreatedAt,
                    ExpiredAt = payment.ExpiredAt,
                    IsPaid = true,
                    RemainingSeconds = 0
                }, "Payment verified successfully.");
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<ApiResponse<PaymentStatusResponse>> ProcessSuccessCallbackAsync(string orderCode)
        {
            try
            {
                var order = await _orderRepository.GetByOrderCodeAsync(orderCode);
                if (order == null)
                    return ApiResponse<PaymentStatusResponse>.ErrorResponse("Order not found.");

                var payment = order.Payment;
                if (payment == null)
                    return ApiResponse<PaymentStatusResponse>.ErrorResponse("Payment record not found.");

                if (payment.PaymentMethod != "SEPAY")
                    return ApiResponse<PaymentStatusResponse>.ErrorResponse("Order does not use SEPAY payment method.");

                // Idempotent: already completed
                if (payment.Status == "COMPLETED")
                {
                    _logger.LogInformation("ProcessSuccessCallback: Payment already completed for order {OrderCode}", orderCode);
                    return ApiResponse<PaymentStatusResponse>.SuccessResponse(new PaymentStatusResponse
                    {
                        OrderId = order.Id,
                        OrderCode = order.OrderCode,
                        PaymentMethod = payment.PaymentMethod,
                        Amount = payment.Amount,
                        Status = "COMPLETED",
                        TransactionId = payment.TransactionId,
                        PaidAt = payment.PaidAt,
                        CreatedAt = payment.CreatedAt,
                        ExpiredAt = payment.ExpiredAt,
                        IsPaid = true,
                        RemainingSeconds = 0
                    }, "Payment already completed.");
                }

                // Only process PENDING payments
                if (payment.Status != "PENDING")
                    return ApiResponse<PaymentStatusResponse>.ErrorResponse(
                        $"Cannot process payment with status '{payment.Status}'.");

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Mark payment as COMPLETED
                    payment.Status = "COMPLETED";
                    payment.PaidAt = DateTime.Now;
                    payment.TransactionId = payment.TransactionId ?? $"CALLBACK-{DateTime.Now:yyyyMMddHHmmss}";
                    await _paymentRepository.UpdateAsync(payment);

                    // Auto-confirm order if PENDING
                    if (order.Status == "PENDING")
                    {
                        order.Status = "CONFIRMED";
                        order.ConfirmedAt = DateTime.Now;
                        await _orderRepository.UpdateAsync(order);
                        _logger.LogInformation("Order {OrderCode} auto-confirmed after success callback", orderCode);
                    }

                    await transaction.CommitAsync();

                    _logger.LogInformation(
                        "ProcessSuccessCallback: Payment completed for order {OrderCode}", orderCode);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error processing success callback for order {OrderCode}", orderCode);
                    throw;
                }

                // Send notification (outside transaction)
                try
                {
                    await _notificationRepository.CreateAsync(new Notification
                    {
                        UserId = order.UserId,
                        Type = "PAYMENT",
                        Title = "Thanh toán thành công",
                        Message = $"Thanh toán đơn hàng {orderCode} đã được xác nhận. Số tiền: {payment.Amount:N0}₫",
                        Data = $"{{\"orderId\":{order.Id}}}",
                        IsRead = false,
                        CreatedAt = DateTime.Now
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create notification for order {OrderCode}", orderCode);
                }

                return ApiResponse<PaymentStatusResponse>.SuccessResponse(new PaymentStatusResponse
                {
                    OrderId = order.Id,
                    OrderCode = order.OrderCode,
                    PaymentMethod = payment.PaymentMethod,
                    Amount = payment.Amount,
                    Status = "COMPLETED",
                    TransactionId = payment.TransactionId,
                    PaidAt = payment.PaidAt,
                    CreatedAt = payment.CreatedAt,
                    ExpiredAt = payment.ExpiredAt,
                    IsPaid = true,
                    RemainingSeconds = 0
                }, "Payment processed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in ProcessSuccessCallbackAsync for order {OrderCode}", orderCode);
                return ApiResponse<PaymentStatusResponse>.ErrorResponse($"System error: {ex.Message}");
            }
        }

        #region Helpers

        private string? ExtractOrderCode(string? content)
        {
            if (string.IsNullOrEmpty(content))
                return null;

            var match = System.Text.RegularExpressions.Regex.Match(
                content, @"SEVQR\d{8}\d{4}");

            return match.Success ? match.Value : null;
        }

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
