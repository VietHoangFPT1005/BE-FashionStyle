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

            // 2. Get user body profile from DB
            var bodyProfile = await _context.UserBodyProfiles
                .FirstOrDefaultAsync(bp => bp.UserId == userId);

            // 3. Get chat history if session exists
            var chatHistory = new List<ChatAiHistory>();
            if (request.SessionId != null)
            {
                chatHistory = await _chatRepository.GetBySessionIdAsync(sessionId, userId);
            }

            // 4. Extract body info from entire conversation (history + current message)
            var allUserMessages = chatHistory
                .Where(m => m.Role == "user")
                .Select(m => m.Content)
                .Append(request.Message)
                .ToList();

            var conversationBody = ExtractBodyFromConversation(allUserMessages);

            // Merge: DB profile takes base, conversation overrides missing fields
            var effectiveBody = MergeBodyProfile(bodyProfile, conversationBody);

            // Auto-save new measurements discovered in conversation to DB
            if (conversationBody.HasAnyMeasurement)
                await AutoSaveBodyProfileAsync(userId, bodyProfile, conversationBody);

            // 5. Build enriched AI context (queries products filtered by gender/body)
            var (systemPrompt, productsWithGuides) = await BuildEnrichedContextAsync(effectiveBody, conversationBody.Gender);

            // 6. Build conversation messages for Gemini
            var conversationParts = new List<object>();

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
                    string? recommendedSize = null;
                    if (effectiveBody != null && p.SizeGuides.Any())
                        recommendedSize = FindBestSize(effectiveBody, p.SizeGuides.ToList());

                    return new ChatSuggestedProduct
                    {
                        ProductId = p.Id,
                        Name = p.Name,
                        Price = p.Price,
                        SalePrice = p.SalePrice,
                        RecommendedSize = recommendedSize,
                        PrimaryImage = p.ProductImages
                            .FirstOrDefault(img => img.IsPrimary == true)?.ImageUrl
                            ?? p.ProductImages.FirstOrDefault()?.ImageUrl
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

        #region Body Extraction Helpers

        // DTO to hold body measurements extracted from conversation
        private class ConversationBodyData
        {
            public decimal? Height { get; set; }
            public decimal? Weight { get; set; }
            public decimal? Bust { get; set; }
            public decimal? Waist { get; set; }
            public decimal? Hips { get; set; }
            public string? Gender { get; set; } // "nam" or "nữ"

            public bool HasAnyMeasurement =>
                Height.HasValue || Weight.HasValue || Bust.HasValue ||
                Waist.HasValue || Hips.HasValue || !string.IsNullOrEmpty(Gender);
        }

        private ConversationBodyData ExtractBodyFromConversation(List<string> userMessages)
        {
            var data = new ConversationBodyData();
            var fullText = string.Join(" ", userMessages).ToLower();

            // Height: "cao 179cm", "179 cm", "1m79", "1.79m"
            var heightPatterns = new[]
            {
                @"cao\s*(\d{2,3})\s*cm",
                @"(\d{3})\s*cm",
                @"1[m.](\d{2})\b",   // 1m79, 1.79
                @"(\d{3})\s*m\b"
            };
            foreach (var pattern in heightPatterns)
            {
                var m = Regex.Match(fullText, pattern);
                if (m.Success && decimal.TryParse(m.Groups[1].Value, out decimal h))
                {
                    // Convert 1m79 → 179
                    if (pattern.Contains("1[m.]") && h < 100) h += 100;
                    if (h >= 140 && h <= 220) { data.Height = h; break; }
                }
            }

            // Weight: "nặng 86kg", "86 kg", "cân 86"
            var weightMatch = Regex.Match(fullText, @"(?:n[aặ]ng|c[aâ]n|w[ei]ght)?\s*(\d{2,3})\s*kg");
            if (weightMatch.Success && decimal.TryParse(weightMatch.Groups[1].Value, out decimal w) && w >= 30 && w <= 200)
                data.Weight = w;

            // Bust / Waist / Hips: "ngực 90", "eo 70", "hông 95"
            var bustMatch = Regex.Match(fullText, @"(?:ng[uưự]c|bust|b)\s*[=:]?\s*(\d{2,3})");
            if (bustMatch.Success && decimal.TryParse(bustMatch.Groups[1].Value, out decimal bust) && bust >= 60 && bust <= 150)
                data.Bust = bust;

            var waistMatch = Regex.Match(fullText, @"(?:eo|waist)\s*[=:]?\s*(\d{2,3})");
            if (waistMatch.Success && decimal.TryParse(waistMatch.Groups[1].Value, out decimal waist) && waist >= 50 && waist <= 130)
                data.Waist = waist;

            var hipsMatch = Regex.Match(fullText, @"(?:h[oôồ]ng|mong|hips?)\s*[=:]?\s*(\d{2,3})");
            if (hipsMatch.Success && decimal.TryParse(hipsMatch.Groups[1].Value, out decimal hips) && hips >= 60 && hips <= 150)
                data.Hips = hips;

            // Gender
            if (Regex.IsMatch(fullText, @"\bt[oô]i\s+l[aà]\s+nam\b|\bnam\s+gi[oớ]i\b|\bcon\s+trai\b|\bgioi\s+tinh\s*[=:]\s*nam"))
                data.Gender = "nam";
            else if (Regex.IsMatch(fullText, @"\bt[oô]i\s+l[aà]\s+n[uữ]\b|\bn[uữ]\s+gi[oớ]i\b|\bcon\s+g[aá]i\b|\bgioi\s+tinh\s*[=:]\s*n[uữ]"))
                data.Gender = "nữ";

            return data;
        }

        private UserBodyProfile? MergeBodyProfile(UserBodyProfile? dbProfile, ConversationBodyData conv)
        {
            if (!conv.HasAnyMeasurement) return dbProfile;

            var merged = new UserBodyProfile
            {
                UserId = dbProfile?.UserId ?? 0,
                Height = conv.Height ?? dbProfile?.Height,
                Weight = conv.Weight ?? dbProfile?.Weight,
                Bust = conv.Bust ?? dbProfile?.Bust,
                Waist = conv.Waist ?? dbProfile?.Waist,
                Hips = conv.Hips ?? dbProfile?.Hips,
                BodyShape = dbProfile?.BodyShape,
                FitPreference = dbProfile?.FitPreference
            };

            return merged;
        }

        private async Task AutoSaveBodyProfileAsync(int userId, UserBodyProfile? existing, ConversationBodyData conv)
        {
            try
            {
                if (existing == null)
                {
                    var newProfile = new UserBodyProfile
                    {
                        UserId = userId,
                        Height = conv.Height,
                        Weight = conv.Weight,
                        Bust = conv.Bust,
                        Waist = conv.Waist,
                        Hips = conv.Hips,
                        UpdatedAt = DateTime.Now
                    };
                    _context.UserBodyProfiles.Add(newProfile);
                }
                else
                {
                    if (conv.Height.HasValue) existing.Height = conv.Height;
                    if (conv.Weight.HasValue) existing.Weight = conv.Weight;
                    if (conv.Bust.HasValue) existing.Bust = conv.Bust;
                    if (conv.Waist.HasValue) existing.Waist = conv.Waist;
                    if (conv.Hips.HasValue) existing.Hips = conv.Hips;
                    existing.UpdatedAt = DateTime.Now;
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not auto-save body profile for user {UserId}", userId);
            }
        }

        #endregion

        #region Helpers

        private async Task<(string prompt, List<Product> products)> BuildEnrichedContextAsync(
            UserBodyProfile? bodyProfile, string? genderFilter)
        {
            // 1. Load products - filter by gender if known
            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.SizeGuides)
                .Include(p => p.ProductVariants.Where(v => v.IsActive == true))
                .Where(p => p.IsActive == true && p.IsDeleted != true)
                .Where(p => p.ProductVariants.Any(v => (v.StockQuantity ?? 0) > 0));

            // Filter by gender when known
            if (!string.IsNullOrEmpty(genderFilter))
            {
                var g = genderFilter.ToLower();
                if (g == "nam" || g == "male")
                    query = query.Where(p => p.Gender == null || p.Gender == "" || p.Gender.Contains("Nam") || p.Gender.Contains("Unisex"));
                else if (g == "nữ" || g == "nu" || g == "female")
                    query = query.Where(p => p.Gender == null || p.Gender == "" || p.Gender.Contains("Nữ") || p.Gender.Contains("Nu") || p.Gender.Contains("Unisex"));
            }

            var products = await query
                .OrderByDescending(p => p.IsFeatured)
                .ThenByDescending(p => p.SoldCount)
                .Take(25)
                .ToListAsync();

            var sb = new StringBuilder();

            // === PERSONA & QUY TẮC PHẢN HỒI ===
            sb.AppendLine("Bạn là FashionStyle AI - chuyên gia tư vấn thời trang Việt Nam.");
            sb.AppendLine("Xưng 'mình', gọi khách 'bạn'. Trả lời tiếng Việt, thân thiện, có emoji.");
            sb.AppendLine();
            sb.AppendLine("⚠️ QUY TẮC BẮT BUỘC - PHẢI TUÂN THỦ TUYỆT ĐỐI:");
            sb.AppendLine("1. Khi gợi ý sản phẩm: LUÔN ghi [ID:X] ngay sau tên sản phẩm. Ví dụ: 'Áo Thun Basic [ID:5]'");
            sb.AppendLine("2. Khi biết chiều cao/cân nặng: GỢI Ý NGAY 3-5 sản phẩm phù hợp ở cuối tin nhắn.");
            sb.AppendLine("3. Mỗi sản phẩm viết 1 dòng ngắn gọn: Tên [ID:X] - Size phù hợp - lý do ngắn.");
            sb.AppendLine("4. KHÔNG mô tả dài dòng từng sản phẩm - hệ thống sẽ tự hiển thị ảnh, giá, size bên dưới.");
            sb.AppendLine("5. Không dùng từ miệt thị vóc dáng. Luôn tích cực và khuyến khích.");
            sb.AppendLine("6. Nếu khách chưa có thông số: hỏi chiều cao, cân nặng (và 3 vòng nếu là nữ).");
            sb.AppendLine("Xu hướng 2025: oversize, wide-leg, tone-on-tone, minimal.");
            sb.AppendLine();

            // === THÔNG TIN CƠ THỂ KHÁCH HÀNG ===
            if (bodyProfile != null)
            {
                var info = new List<string>();
                if (!string.IsNullOrEmpty(genderFilter)) info.Add($"giới tính: {genderFilter}");
                if (bodyProfile.Height.HasValue) info.Add($"cao {bodyProfile.Height}cm");
                if (bodyProfile.Weight.HasValue) info.Add($"nặng {bodyProfile.Weight}kg");
                if (bodyProfile.Bust.HasValue) info.Add($"ngực {bodyProfile.Bust}cm");
                if (bodyProfile.Waist.HasValue) info.Add($"eo {bodyProfile.Waist}cm");
                if (bodyProfile.Hips.HasValue) info.Add($"hông {bodyProfile.Hips}cm");
                if (!string.IsNullOrEmpty(bodyProfile.BodyShape)) info.Add($"dáng người: {bodyProfile.BodyShape}");

                sb.AppendLine($"📊 THÔNG SỐ KHÁCH HÀNG: {string.Join(", ", info)}");
                sb.AppendLine("=> Dựa vào thông số này để đối chiếu bảng size và GỢI Ý NGAY sản phẩm phù hợp kèm [ID:X].");
            }
            else if (!string.IsNullOrEmpty(genderFilter))
            {
                sb.AppendLine($"📊 THÔNG SỐ KHÁCH HÀNG: giới tính {genderFilter}. Chưa có chiều cao/cân nặng - hãy hỏi thêm.");
            }
            else
            {
                sb.AppendLine("📊 THÔNG SỐ KHÁCH HÀNG: Chưa có. Hỏi chiều cao, cân nặng trước khi tư vấn size.");
            }
            sb.AppendLine();

            // === DANH MỤC SẢN PHẨM ===
            sb.AppendLine($"🛍️ DANH MỤC SẢN PHẨM (còn hàng{(!string.IsNullOrEmpty(genderFilter) ? $", đã lọc cho {genderFilter}" : "")}):");
            foreach (var p in products)
            {
                var price = p.SalePrice.HasValue && p.SalePrice < p.Price
                    ? $"{p.SalePrice:N0}đ (gốc {p.Price:N0}đ)"
                    : $"{p.Price:N0}đ";

                var catName = p.Category?.Name ?? "";
                var genderTag = !string.IsNullOrEmpty(p.Gender) ? $" | {p.Gender}" : "";
                var brand = !string.IsNullOrEmpty(p.BrandName) ? $" | {p.BrandName}" : "";

                sb.AppendLine($"[ID:{p.Id}] {p.Name} | {price} | {catName}{genderTag}{brand}");

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
                                parts.Add($"ngực{sg.MinBust}-{sg.MaxBust}");
                            if (sg.MinWaist.HasValue && sg.MaxWaist.HasValue)
                                parts.Add($"eo{sg.MinWaist}-{sg.MaxWaist}");
                            return parts.Any() ? $"{sg.SizeName}({string.Join(",", parts)})" : sg.SizeName;
                        });
                    sb.AppendLine($"  Bảng size: {string.Join(" | ", sizeList)}");
                }

                var colors = p.ProductVariants
                    .Where(v => (v.StockQuantity ?? 0) > 0)
                    .GroupBy(v => v.Color)
                    .Select(g => g.Key)
                    .Where(c => !string.IsNullOrEmpty(c));
                if (colors.Any())
                    sb.AppendLine($"  Màu sắc: {string.Join(", ", colors)}");
            }

            return (sb.ToString(), products);
        }

        private async Task<string> CallGeminiApiAsync(string systemPrompt, List<object> conversationParts)
        {
            var httpClient = _httpClientFactory.CreateClient();
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_geminiSettings.Model}:generateContent?key={_geminiSettings.ApiKey}";

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

            // Extract all [ID:X] from AI response - trust the AI's suggestions
            var idMatches = Regex.Matches(aiResponse, @"\[ID:(\d+)\]");
            foreach (Match match in idMatches)
            {
                if (int.TryParse(match.Groups[1].Value, out int id) && !ids.Contains(id))
                    ids.Add(id);
            }

            // Fallback: match by product name if no IDs found
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
