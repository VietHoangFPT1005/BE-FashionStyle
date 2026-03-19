using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MV.ApplicationLayer.ServiceInterfaces;
using MV.DomainLayer.Configuration;
using MV.DomainLayer.DTOs.Chat.Request;
using MV.DomainLayer.DTOs.Chat.Response;
using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.Entities;
using MV.InfrastructureLayer.DBContext;
using MV.InfrastructureLayer.Interfaces;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MV.ApplicationLayer.Services
{
    public class ChatAiService : IChatAiService
    {
        private readonly FashionDbContext _context;
        private readonly IChatAiHistoryRepository _chatRepository;
        private readonly GeminiSettings _geminiSettings;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ChatAiService> _logger;

        public ChatAiService(
            FashionDbContext context,
            IChatAiHistoryRepository chatRepository,
            IOptions<GeminiSettings> geminiSettings,
            IHttpClientFactory httpClientFactory,
            ILogger<ChatAiService> logger)
        {
            _context = context;
            _chatRepository = chatRepository;
            _geminiSettings = geminiSettings.Value;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        // ==================== API 7: Send Message to AI Chat ====================
        public async Task<ApiResponse<ChatMessageResponse>> SendMessageAsync(
            int userId, ChatMessageRequest request)
        {
            // 1. Generate or use existing sessionId
            var sessionId = request.SessionId ?? Guid.NewGuid().ToString();

            // 2. Get user body profile
            var bodyProfile = await _context.UserBodyProfiles
                .FirstOrDefaultAsync(bp => bp.UserId == userId);

            // 3. Get chat history if session exists
            var chatHistory = new List<ChatAiHistory>();
            if (request.SessionId != null)
            {
                chatHistory = await _chatRepository.GetBySessionIdAsync(sessionId, userId);
            }

            // 4. Build enriched AI context (queries products, categories, reviews from DB)
            var (systemPrompt, productsWithGuides) = await BuildEnrichedContextAsync(bodyProfile);

            // 6. Build conversation messages for Gemini
            var conversationParts = new List<object>();

            // Add system instruction and history
            foreach (var msg in chatHistory)
            {
                conversationParts.Add(new
                {
                    role = msg.Role == "user" ? "user" : "model",
                    parts = new[] { new { text = msg.Content } }
                });
            }

            // Add current user message
            conversationParts.Add(new
            {
                role = "user",
                parts = new[] { new { text = request.Message } }
            });

            // 7. Call Gemini API
            string aiResponse;
            try
            {
                aiResponse = await CallGeminiApiAsync(systemPrompt, conversationParts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Gemini API: {Message}", ex.Message);

                // Hiển thị message thân thiện với user, không lộ lỗi kỹ thuật
                if (ex.Message == "RATE_LIMIT_EXCEEDED")
                    aiResponse = "Mình đang có quá nhiều người dùng cùng lúc 😅 Bạn vui lòng thử lại sau vài giây nhé! 🙏";
                else
                    aiResponse = "Xin lỗi bạn, mình đang gặp sự cố kỹ thuật tạm thời. Vui lòng thử lại sau ít phút nhé! 🙏";
            }

            // 8. Extract suggested product IDs from AI response
            var suggestedProductIds = ExtractProductIds(aiResponse, productsWithGuides);

            // 9. Save chat history (user message + AI response)
            var chatEntries = new List<ChatAiHistory>
            {
                new ChatAiHistory
                {
                    UserId = userId,
                    Role = "user",
                    Content = request.Message,
                    SessionId = sessionId,
                    CreatedAt = DateTime.Now
                },
                new ChatAiHistory
                {
                    UserId = userId,
                    Role = "assistant",
                    Content = aiResponse,
                    SuggestedProductIds = suggestedProductIds.Any() ? suggestedProductIds : null,
                    SessionId = sessionId,
                    CreatedAt = DateTime.Now
                }
            };

            await _chatRepository.CreateRangeAsync(chatEntries);

            // 10. Build suggested products response
            var suggestedProducts = new List<ChatSuggestedProduct>();
            if (suggestedProductIds.Any())
            {
                var products = await _context.Products
                    .Include(p => p.ProductImages)
                    .Include(p => p.SizeGuides)
                    .Where(p => suggestedProductIds.Contains(p.Id))
                    .ToListAsync();

                suggestedProducts = products.Select(p =>
                {
                    // Determine recommended size based on body profile
                    string? recommendedSize = null;
                    if (bodyProfile != null && p.SizeGuides.Any())
                    {
                        recommendedSize = FindBestSize(bodyProfile, p.SizeGuides.ToList());
                    }

                    return new ChatSuggestedProduct
                    {
                        ProductId = p.Id,
                        Name = p.Name,
                        Price = p.Price,
                        SalePrice = p.SalePrice,
                        RecommendedSize = recommendedSize,
                        PrimaryImage = p.ProductImages
                            .FirstOrDefault(img => img.IsPrimary == true)?.ImageUrl
                    };
                }).ToList();
            }

            var response = new ChatMessageResponse
            {
                SessionId = sessionId,
                Role = "assistant",
                Content = aiResponse,
                SuggestedProducts = suggestedProducts.Any() ? suggestedProducts : null,
                CreatedAt = DateTime.Now
            };

            return ApiResponse<ChatMessageResponse>.SuccessResponse(response);
        }

        // ==================== API 8: Get Chat Sessions ====================
        public async Task<ApiResponse<List<ChatSessionListResponse>>> GetSessionsAsync(int userId)
        {
            var sessions = await _chatRepository.GetSessionsByUserIdAsync(userId);

            var response = sessions.Select(s => new ChatSessionListResponse
            {
                SessionId = s.SessionId,
                LastMessage = s.LastMessage,
                MessageCount = s.MessageCount,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt
            }).ToList();

            return ApiResponse<List<ChatSessionListResponse>>.SuccessResponse(response);
        }

        // ==================== API 9: Get Chat Session History ====================
        public async Task<ApiResponse<ChatSessionDetailResponse>> GetSessionHistoryAsync(
            int userId, string sessionId)
        {
            // Verify session belongs to user
            var belongs = await _chatRepository.SessionBelongsToUserAsync(sessionId, userId);
            if (!belongs)
                return ApiResponse<ChatSessionDetailResponse>.ErrorResponse("Chat session not found.");

            var messages = await _chatRepository.GetBySessionIdAsync(sessionId, userId);

            var response = new ChatSessionDetailResponse
            {
                SessionId = sessionId,
                Messages = messages.Select(m => new ChatHistoryMessage
                {
                    Role = m.Role,
                    Content = m.Content,
                    SuggestedProductIds = m.SuggestedProductIds,
                    CreatedAt = m.CreatedAt
                }).ToList()
            };

            return ApiResponse<ChatSessionDetailResponse>.SuccessResponse(response);
        }

        // ==================== API 10: Delete Chat Session ====================
        public async Task<ApiResponse<object>> DeleteSessionAsync(int userId, string sessionId)
        {
            var belongs = await _chatRepository.SessionBelongsToUserAsync(sessionId, userId);
            if (!belongs)
                return ApiResponse<object>.ErrorResponse("Chat session not found.");

            var deletedCount = await _chatRepository.DeleteBySessionIdAndUserIdAsync(sessionId, userId);

            return ApiResponse<object>.SuccessResponse($"Chat session deleted. {deletedCount} messages removed.");
        }

        #region Helpers

        private async Task<(string prompt, List<Product> products)> BuildEnrichedContextAsync(
            UserBodyProfile? bodyProfile)
        {
            // 1. Load products - giảm xuống 20, chỉ lấy thông tin cần thiết
            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.SizeGuides)
                .Include(p => p.ProductVariants.Where(v => v.IsActive == true))
                .Where(p => p.IsActive == true && p.IsDeleted != true)
                .Where(p => p.ProductVariants.Any(v => (v.StockQuantity ?? 0) > 0))
                .OrderByDescending(p => p.IsFeatured)
                .ThenByDescending(p => p.SoldCount)
                .Take(20)
                .ToListAsync();

            var sb = new StringBuilder();

            // === PERSONA (ngắn gọn) ===
            sb.AppendLine("Ban la FashionStyle AI - tu van thoi trang Viet Nam. Xung 'minh', goi khach 'ban'. Tra loi tieng Viet, ngan gon, them emoji.");
            sb.AppendLine("Khi goi y san pham: PHAI ghi [ID:X] sau ten san pham. Goi y toi da 3-5 san pham.");
            sb.AppendLine("Khong dung tu miet thi ngoai hinh. Neu khach chua co so do: hoi chieu cao, can nang, 3 vong.");
            sb.AppendLine("Xu huong 2024-2025: oversize, wide-leg, tone-on-tone, minimal.");
            sb.AppendLine();

            // === THONG TIN KHACH HANG ===
            if (bodyProfile != null)
            {
                var info = new List<string>();
                if (bodyProfile.Height.HasValue) info.Add($"cao {bodyProfile.Height}cm");
                if (bodyProfile.Weight.HasValue) info.Add($"nang {bodyProfile.Weight}kg");
                if (bodyProfile.Bust.HasValue) info.Add($"nguc {bodyProfile.Bust}cm");
                if (bodyProfile.Waist.HasValue) info.Add($"eo {bodyProfile.Waist}cm");
                if (bodyProfile.Hips.HasValue) info.Add($"hong {bodyProfile.Hips}cm");
                if (!string.IsNullOrEmpty(bodyProfile.BodyShape)) info.Add($"dang {bodyProfile.BodyShape}");
                if (info.Any()) sb.AppendLine($"Khach hang: {string.Join(", ", info)}. Tu van size cu the theo bang size.");
            }
            else
            {
                sb.AppendLine("Khach chua co so do. Hoi de tu van chinh xac hon.");
            }
            sb.AppendLine();

            // === CATALOG SAN PHAM ===
            sb.AppendLine("CATALOG (con hang):");
            foreach (var p in products)
            {
                var price = p.SalePrice.HasValue && p.SalePrice < p.Price
                    ? $"{p.SalePrice:N0}d(giam {p.Price:N0}d)"
                    : $"{p.Price:N0}d";

                var catName = p.Category?.Name ?? "";
                var gender = !string.IsNullOrEmpty(p.Gender) ? $"|{p.Gender}" : "";
                var brand = !string.IsNullOrEmpty(p.BrandName) ? $"|{p.BrandName}" : "";
                var material = !string.IsNullOrEmpty(p.Material) ? $"|{p.Material}" : "";

                sb.AppendLine($"[ID:{p.Id}]{p.Name}|{price}|{catName}{gender}{brand}{material}");

                // Sizes - chỉ hiển thị tên size và range cân nặng (quan trọng nhất)
                if (p.SizeGuides.Any())
                {
                    var sizeList = p.SizeGuides
                        .OrderBy(s => s.SizeName)
                        .Select(sg =>
                        {
                            var parts = new List<string>();
                            if (sg.MinWeight.HasValue && sg.MaxWeight.HasValue)
                                parts.Add($"{sg.MinWeight}-{sg.MaxWeight}kg");
                            if (sg.MinBust.HasValue && sg.MaxBust.HasValue)
                                parts.Add($"nguc{sg.MinBust}-{sg.MaxBust}");
                            if (sg.MinWaist.HasValue && sg.MaxWaist.HasValue)
                                parts.Add($"eo{sg.MinWaist}-{sg.MaxWaist}");
                            return parts.Any() ? $"{sg.SizeName}({string.Join(",", parts)})" : sg.SizeName;
                        });
                    sb.AppendLine($"  Size: {string.Join(" | ", sizeList)}");
                }

                // Màu sắc có hàng
                var colors = p.ProductVariants
                    .Where(v => (v.StockQuantity ?? 0) > 0)
                    .GroupBy(v => v.Color)
                    .Select(g => g.Key)
                    .Where(c => !string.IsNullOrEmpty(c));
                if (colors.Any())
                    sb.AppendLine($"  Mau: {string.Join(", ", colors)}");
            }

            return (sb.ToString(), products);
        }

        private async Task<string> CallGeminiApiAsync(string systemPrompt, List<object> conversationParts)
        {
            var httpClient = _httpClientFactory.CreateClient();
            var url = $"https://generativelanguage.googleapis.com/v1/models/{_geminiSettings.Model}:generateContent?key={_geminiSettings.ApiKey}";

            var requestBody = new
            {
                system_instruction = new
                {
                    parts = new[] { new { text = systemPrompt } }
                },
                contents = conversationParts,
                generationConfig = new
                {
                    temperature = _geminiSettings.Temperature,
                    maxOutputTokens = _geminiSettings.MaxTokens
                }
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Retry tối đa 2 lần nếu gặp 429
            int maxRetries = 2;
            int retryDelay = 5000; // 5 giây

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                if (attempt > 0)
                {
                    _logger.LogWarning("Gemini 429 - retry {Attempt}/{Max} sau {Delay}ms", attempt, maxRetries, retryDelay);
                    await Task.Delay(retryDelay);
                    retryDelay *= 2; // exponential backoff
                    httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                }

                var response = await httpClient.PostAsync(url, httpContent);

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests && attempt < maxRetries)
                {
                    var errBody = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Gemini API HTTP 429: {Body}", errBody);
                    continue; // retry
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Gemini API HTTP {StatusCode}: {Body}", (int)response.StatusCode, errorBody);

                    // 429 sau khi hết retry
                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                        throw new Exception("RATE_LIMIT_EXCEEDED");

                    throw new Exception($"Gemini API error {(int)response.StatusCode}: {errorBody}");
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseBody);

                var candidates = doc.RootElement.GetProperty("candidates");
                if (candidates.GetArrayLength() > 0)
                {
                    var content = candidates[0].GetProperty("content");
                    var parts = content.GetProperty("parts");
                    if (parts.GetArrayLength() > 0)
                        return parts[0].GetProperty("text").GetString() ?? "Xin lỗi, mình không hiểu câu hỏi này. Bạn thử hỏi lại nhé! 😊";
                }

                return "Xin lỗi, mình chưa có câu trả lời phù hợp. Bạn hỏi lại theo cách khác được không? 😊";
            }

            throw new Exception("RATE_LIMIT_EXCEEDED");
        }

        private List<int> ExtractProductIds(string aiResponse, List<Product> products)
        {
            var ids = new List<int>();

            // Try to extract IDs from format [ID:X]
            var idMatches = Regex.Matches(aiResponse, @"\[ID:(\d+)\]");
            foreach (Match match in idMatches)
            {
                if (int.TryParse(match.Groups[1].Value, out int id))
                {
                    if (products.Any(p => p.Id == id) && !ids.Contains(id))
                        ids.Add(id);
                }
            }

            // Also try to match product names mentioned in the response
            if (!ids.Any())
            {
                foreach (var product in products)
                {
                    if (aiResponse.Contains(product.Name, StringComparison.OrdinalIgnoreCase)
                        && !ids.Contains(product.Id))
                    {
                        ids.Add(product.Id);
                    }
                }
            }

            return ids.Take(5).ToList();
        }

        private string? FindBestSize(UserBodyProfile profile, List<SizeGuide> sizeGuides)
        {
            string? bestSize = null;
            double bestScore = -1;

            foreach (var sg in sizeGuides)
            {
                double score = 0;
                int measurements = 0;

                if (profile.Weight.HasValue && sg.MinWeight.HasValue && sg.MaxWeight.HasValue)
                {
                    if (profile.Weight >= sg.MinWeight && profile.Weight <= sg.MaxWeight)
                        score += 1;
                    measurements++;
                }

                if (profile.Bust.HasValue && sg.MinBust.HasValue && sg.MaxBust.HasValue)
                {
                    if (profile.Bust >= sg.MinBust && profile.Bust <= sg.MaxBust)
                        score += 1;
                    measurements++;
                }

                if (profile.Waist.HasValue && sg.MinWaist.HasValue && sg.MaxWaist.HasValue)
                {
                    if (profile.Waist >= sg.MinWaist && profile.Waist <= sg.MaxWaist)
                        score += 1;
                    measurements++;
                }

                if (profile.Hips.HasValue && sg.MinHips.HasValue && sg.MaxHips.HasValue)
                {
                    if (profile.Hips >= sg.MinHips && profile.Hips <= sg.MaxHips)
                        score += 1;
                    measurements++;
                }

                if (measurements > 0)
                {
                    var normalizedScore = score / measurements;
                    if (normalizedScore > bestScore)
                    {
                        bestScore = normalizedScore;
                        bestSize = sg.SizeName;
                    }
                }
            }

            return bestSize;
        }

        #endregion
    }
}
