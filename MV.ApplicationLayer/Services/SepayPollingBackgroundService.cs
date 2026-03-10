using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MV.ApplicationLayer.ServiceInterfaces;
using MV.DomainLayer.Configuration;
using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.DTOs.Payment.Response;
using MV.InfrastructureLayer.Interfaces;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MV.ApplicationLayer.Services
{
    /// <summary>
    /// Background service polling SePay API mỗi 15 giây để kiểm tra giao dịch mới.
    /// Khi phát hiện giao dịch khớp với order PENDING → tự động cập nhật COMPLETED.
    /// Giải pháp thay thế/bổ sung webhook khi chạy trên localhost hoặc webhook bị fail.
    /// </summary>
    public class SepayPollingBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SepayPollingBackgroundService> _logger;
        private readonly string _apiToken;
        private readonly string _apiBaseUrl;
        private readonly string _accountNumber;
        private readonly HttpClient _httpClient;
        private readonly TimeSpan _interval = TimeSpan.FromSeconds(15);
        private readonly TimeSpan _errorBackoffDelay = TimeSpan.FromSeconds(30);

        // Track giao dịch đã match thành công → không cần thử lại
        private readonly HashSet<long> _matchedTransactionIds = new();
        // Track ID cao nhất để phân biệt giao dịch mới (chỉ dùng cho amount matching)
        private long _lastProcessedId = 0;

        public SepayPollingBackgroundService(
            IServiceProvider serviceProvider,
            IOptions<SePaySettings> sePaySettings,
            ILogger<SepayPollingBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _httpClient = new HttpClient();

            var settings = sePaySettings.Value;
            _apiToken = settings.ApiToken;
            _apiBaseUrl = string.IsNullOrEmpty(settings.ApiBaseUrl)
                ? "https://my.sepay.vn/"
                : settings.ApiBaseUrl;
            _accountNumber = settings.AccountNumber;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                if (string.IsNullOrEmpty(_apiToken))
                {
                    _logger.LogWarning("SePay ApiToken is not configured. SepayPollingService will not start.");
                    return;
                }

                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _apiToken);

                _logger.LogInformation("SepayPollingBackgroundService started - polling every {Interval}s", _interval.TotalSeconds);

                // Wait 10 seconds for app startup
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

                while (!stoppingToken.IsCancellationRequested)
                {
                    var delay = _interval;
                    try
                    {
                        await PollTransactionsAsync(stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in SepayPollingBackgroundService - backing off for {Seconds}s", _errorBackoffDelay.TotalSeconds);
                        delay = _errorBackoffDelay;
                    }

                    await Task.Delay(delay, stoppingToken);
                }

                _logger.LogInformation("SepayPollingBackgroundService stopped");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("SepayPollingBackgroundService cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SepayPollingBackgroundService fatal error");
            }
        }

        private async Task PollTransactionsAsync(CancellationToken ct)
        {
            // Call SePay API to get recent transactions (20 most recent)
            var url = $"{_apiBaseUrl.TrimEnd('/')}/userapi/transactions/list?limit=20";
            if (!string.IsNullOrEmpty(_accountNumber))
            {
                url += $"&account_number={_accountNumber}";
            }

            var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("SePay API returned HTTP {StatusCode}", response.StatusCode);
                return;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var sepayResponse = JsonSerializer.Deserialize<SepayTransactionListResponse>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            });

            if (sepayResponse?.Transactions == null || sepayResponse.Transactions.Count == 0)
            {
                _logger.LogDebug("SePay Polling: No transactions found");
                return;
            }

            // Get pending SEPAY payments from DB
            using var scope = _serviceProvider.CreateScope();
            var paymentRepository = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();
            var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();

            var pendingPayments = await paymentRepository.GetPendingSePayPaymentsAsync();
            if (pendingPayments.Count == 0)
            {
                // No pending orders → just update lastProcessedId and skip
                var maxId = sepayResponse.Transactions.Max(t => t.Id);
                if (maxId > _lastProcessedId)
                    _lastProcessedId = maxId;
                return;
            }

            _logger.LogInformation(
                "SePay Polling: Found {TxCount} transactions, {OrderCount} pending payments, lastProcessedId={LastId}",
                sepayResponse.Transactions.Count, pendingPayments.Count, _lastProcessedId);

            foreach (var tx in sepayResponse.Transactions)
            {
                // Skip outgoing transfers or already matched transactions
                if (tx.AmountIn <= 0 || _matchedTransactionIds.Contains(tx.Id))
                    continue;

                var content = tx.TransactionContent ?? "";
                var orderCode = ExtractOrderCode(content);

                if (!string.IsNullOrEmpty(orderCode))
                {
                    // === SEVQR reference found → always try to match (safe because exact match) ===
                    _logger.LogInformation(
                        "SePay Polling: Tx {TxId} has SEVQR ref '{OrderCode}' → trying to match",
                        tx.Id, orderCode);

                    if (await TryCompleteOrder(paymentService, pendingPayments, orderCode, tx))
                    {
                        _matchedTransactionIds.Add(tx.Id);
                    }
                }
                else if (tx.Id > _lastProcessedId)
                {
                    // === No SEVQR ref, only try for NEW transactions (avoid false match by amount) ===
                    _logger.LogInformation(
                        "SePay Polling: New tx {TxId}, AmountIn={AmountIn}, no SEVQR ref, trying amount match...",
                        tx.Id, tx.AmountIn);

                    if (await TryMatchByAmount(paymentService, pendingPayments, tx))
                    {
                        _matchedTransactionIds.Add(tx.Id);
                    }
                }
            }

            // Update lastProcessedId
            var maxTxId = sepayResponse.Transactions.Max(t => t.Id);
            if (maxTxId > _lastProcessedId)
            {
                _lastProcessedId = maxTxId;
                _logger.LogDebug("SePay Polling: Updated lastProcessedId to {LastId}", _lastProcessedId);
            }

            // Cleanup: only keep IDs still in current batch
            _matchedTransactionIds.IntersectWith(sepayResponse.Transactions.Select(t => t.Id));
        }

        /// <summary>
        /// Try to complete order when SEVQR reference is found in transaction content.
        /// </summary>
        private async Task<bool> TryCompleteOrder(
            IPaymentService paymentService,
            List<DomainLayer.Entities.Payment> pendingPayments,
            string orderCode, SepayTransactionItem tx)
        {
            // Find matching payment by order code
            var payment = pendingPayments.FirstOrDefault(p => p.Order?.OrderCode == orderCode);
            if (payment == null)
            {
                _logger.LogDebug("SePay Polling: No pending payment found for order code {OrderCode}", orderCode);
                return false;
            }

            // Verify amount
            if (tx.AmountIn < payment.Amount)
            {
                _logger.LogWarning(
                    "SePay Polling: Amount mismatch for {OrderCode}. Expected={Expected}, Received={Received}",
                    orderCode, payment.Amount, tx.AmountIn);
                return false;
            }

            _logger.LogInformation(
                "SePay Polling: Match by SEVQR ref! OrderCode={OrderCode}, Amount={Amount}",
                orderCode, tx.AmountIn);

            var result = await paymentService.ProcessSuccessCallbackAsync(orderCode);
            LogResult(orderCode, result);
            return result.Success;
        }

        /// <summary>
        /// Match transaction with pending order by exact amount.
        /// Fallback when transfer content doesn't contain SEVQR reference.
        /// </summary>
        private async Task<bool> TryMatchByAmount(
            IPaymentService paymentService,
            List<DomainLayer.Entities.Payment> pendingPayments,
            SepayTransactionItem tx)
        {
            foreach (var payment in pendingPayments)
            {
                if (payment.Order == null) continue;

                // Check exact amount match
                if (payment.Amount != tx.AmountIn) continue;

                // Check payment not expired
                if (payment.ExpiredAt.HasValue && payment.ExpiredAt.Value < DateTime.Now) continue;

                _logger.LogInformation(
                    "SePay Polling: Match by amount! OrderCode={OrderCode}, Amount={Amount}",
                    payment.Order.OrderCode, tx.AmountIn);

                var result = await paymentService.ProcessSuccessCallbackAsync(payment.Order.OrderCode);
                LogResult(payment.Order.OrderCode, result);

                // Matched 1 order → stop (don't match this transaction with another order)
                return result.Success;
            }

            return false;
        }

        private void LogResult(string orderCode, ApiResponse<PaymentStatusResponse> result)
        {
            if (result.Success)
            {
                _logger.LogInformation("SePay Polling: Updated order {OrderCode} → COMPLETED", orderCode);
            }
            else
            {
                _logger.LogWarning("SePay Polling: Could not update order {OrderCode}: {Message}", orderCode, result.Message);
            }
        }

        /// <summary>
        /// Extract order code (format: SEVQR-YYYYMMDD-NNNN) from transfer content.
        /// </summary>
        private static string? ExtractOrderCode(string content)
        {
            if (string.IsNullOrEmpty(content))
                return null;

            var upperContent = content.ToUpper().Replace(" ", "");
            var match = System.Text.RegularExpressions.Regex.Match(
                upperContent, @"SEVQR-\d{8}-\d{4}");

            return match.Success ? match.Value : null;
        }

        public override void Dispose()
        {
            _httpClient.Dispose();
            base.Dispose();
        }
    }

    // DTO for SePay API response
    public class SepayTransactionListResponse
    {
        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("messages")]
        public SepayMessages? Messages { get; set; }

        [JsonPropertyName("transactions")]
        public List<SepayTransactionItem> Transactions { get; set; } = new();
    }

    public class SepayMessages
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }
    }

    public class SepayTransactionItem
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("bank_brand_name")]
        public string? BankBrandName { get; set; }

        [JsonPropertyName("account_number")]
        public string? AccountNumber { get; set; }

        [JsonPropertyName("transaction_date")]
        public string? TransactionDate { get; set; }

        [JsonPropertyName("amount_in")]
        public decimal AmountIn { get; set; }

        [JsonPropertyName("amount_out")]
        public decimal AmountOut { get; set; }

        [JsonPropertyName("accumulated")]
        public decimal Accumulated { get; set; }

        [JsonPropertyName("transaction_content")]
        public string? TransactionContent { get; set; }

        [JsonPropertyName("reference_number")]
        public string? ReferenceNumber { get; set; }

        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("sub_account")]
        public string? SubAccount { get; set; }

        [JsonPropertyName("bank_account_id")]
        public int? BankAccountId { get; set; }
    }
}
