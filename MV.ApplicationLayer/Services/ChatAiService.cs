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
                _logger.LogError(ex, "Error calling Gemini API");
                // Hiển thị lỗi chi tiết để debug - sau khi fix thì đổi lại thành thông báo thân thiện
                aiResponse = $"[Lỗi Gemini API] {ex.Message}";
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
            // ========== QUERY ENRICHED DATA FROM DATABASE ==========

            // 1. Load categories
            var categories = await _context.Categories
                .Where(c => c.IsActive == true)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToListAsync();

            // 2. Load products with full details (Category, SizeGuides, Variants, Images)
            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.SizeGuides)
                .Include(p => p.ProductVariants.Where(v => v.IsActive == true))
                .Include(p => p.ProductImages.Where(img => img.IsPrimary == true))
                .Where(p => p.IsActive == true && p.IsDeleted != true)
                .Where(p => p.ProductVariants.Any(v => (v.StockQuantity ?? 0) > 0))
                .OrderByDescending(p => p.IsFeatured)
                .ThenByDescending(p => p.SoldCount)
                .Take(30)
                .ToListAsync();

            // 3. Load review insights (from buyers with body info for size reference)
            var productIds = products.Select(p => p.Id).ToList();
            var reviewInsights = await _context.ProductReviews
                .Where(r => productIds.Contains(r.ProductId))
                .Where(r => r.ShowBodyInfo == true && r.HeightCm.HasValue && r.WeightKg.HasValue)
                .OrderByDescending(r => r.Rating)
                .Take(20)
                .ToListAsync();

            // ========== BUILD FINE-TUNED SYSTEM PROMPT ==========

            var sb = new StringBuilder();

            // === AI PERSONA ===
            sb.AppendLine("=== VAI TRO ===");
            sb.AppendLine("Ban la 'BigSize Fashion AI' - chuyen gia tu van thoi trang cho nguoi mac size lon tai Viet Nam.");
            sb.AppendLine("Ten thuong hieu: BigSize Fashion.");
            sb.AppendLine("Slogan: 'Your Style, Your Confidence' - Phong cach cua ban, Su tu tin cua ban.");
            sb.AppendLine();

            // === BEHAVIOR RULES ===
            sb.AppendLine("=== QUY TAC GIAO TIEP ===");
            sb.AppendLine("1. Xung ho 'minh' va goi khach 'ban' de tao cam giac than thien, gan gui.");
            sb.AppendLine("2. TUYET DOI khong dung tu miet thi ngoai co (map, beo, to con...). Thay vao do dung: 'dang nguoi day dan', 'voc dang khoe manh', 'phom nguoi thoai mai'.");
            sb.AppendLine("3. Luon tu tin, tich cuc va dong vien khach hang. Moi dang nguoi deu dep!");
            sb.AppendLine("4. Tra loi bang tieng Viet, ngan gon, de hieu, co cam xuc.");
            sb.AppendLine("5. Khi goi y san pham, LUON ghi ro [ID:X] (X la ma san pham) de he thong nhan dien duoc.");
            sb.AppendLine("6. Luon de cap gia ban. Neu co giam gia, ghi ca gia goc va gia khuyen mai.");
            sb.AppendLine("7. Khi tu van size, dua tren BANG SIZE CU THE cua tung san pham, khong doan mo.");
            sb.AppendLine("8. Neu khach chua co so do, hay hoi: chieu cao, can nang, vong nguc, vong eo, vong hong.");
            sb.AppendLine("9. Co the goi y phoi do (ao + quan, phu kien...) neu phu hop voi nhu cau khach.");
            sb.AppendLine("10. Toi da goi y 3-5 san pham moi lan, uu tien san pham phu hop nhat.");
            sb.AppendLine("11. Khi khach hoi ve chat lieu, cam giac mac, hay mo ta chi tiet dua tren thong tin san pham.");
            sb.AppendLine("12. Neu khong co san pham phu hop, hay trung thuc thong bao va goi y khach cho dot hang moi.");
            sb.AppendLine();

            // === USER BODY PROFILE ===
            sb.AppendLine("=== THONG TIN KHACH HANG ===");
            if (bodyProfile != null)
            {
                if (bodyProfile.Height.HasValue) sb.AppendLine($"- Chieu cao: {bodyProfile.Height}cm");
                if (bodyProfile.Weight.HasValue) sb.AppendLine($"- Can nang: {bodyProfile.Weight}kg");
                if (bodyProfile.Bust.HasValue) sb.AppendLine($"- Vong nguc (Bust): {bodyProfile.Bust}cm");
                if (bodyProfile.Waist.HasValue) sb.AppendLine($"- Vong eo (Waist): {bodyProfile.Waist}cm");
                if (bodyProfile.Hips.HasValue) sb.AppendLine($"- Vong hong (Hips): {bodyProfile.Hips}cm");
                if (bodyProfile.Arm.HasValue) sb.AppendLine($"- Vong tay: {bodyProfile.Arm}cm");
                if (bodyProfile.Thigh.HasValue) sb.AppendLine($"- Vong dui: {bodyProfile.Thigh}cm");
                if (!string.IsNullOrEmpty(bodyProfile.BodyShape)) sb.AppendLine($"- Dang nguoi: {bodyProfile.BodyShape}");
                if (!string.IsNullOrEmpty(bodyProfile.FitPreference)) sb.AppendLine($"- Phong cach yeu thich: {bodyProfile.FitPreference}");
                sb.AppendLine("=> Da co so do khach hang. Hay tu van size CU THE dua tren bang size san pham.");
            }
            else
            {
                sb.AppendLine("Khach hang CHUA cap nhat so do co the.");
                sb.AppendLine("=> HAY HOI khach hang ve chieu cao, can nang va so do 3 vong de tu van chinh xac hon.");
            }
            sb.AppendLine();

            // === CATEGORIES ===
            if (categories.Any())
            {
                sb.AppendLine("=== DANH MUC SAN PHAM ===");
                foreach (var cat in categories)
                {
                    var parentName = cat.ParentId.HasValue
                        ? categories.FirstOrDefault(c => c.Id == cat.ParentId)?.Name
                        : null;
                    var parentInfo = parentName != null ? $" (thuoc nhom {parentName})" : "";
                    sb.AppendLine($"- {cat.Name}{parentInfo}");
                }
                sb.AppendLine();
            }

            // === PRODUCTS CATALOG ===
            sb.AppendLine("=== CATALOG SAN PHAM (CON HANG) ===");
            foreach (var p in products)
            {
                // Price info
                var price = p.SalePrice.HasValue && p.SalePrice < p.Price
                    ? $"{p.SalePrice:N0}d (giam tu {p.Price:N0}d)"
                    : $"{p.Price:N0}d";

                sb.AppendLine($"--- [ID:{p.Id}] {p.Name} ---");
                sb.AppendLine($"  Gia: {price}");
                if (p.Category != null) sb.AppendLine($"  Danh muc: {p.Category.Name}");
                if (!string.IsNullOrEmpty(p.Gender)) sb.AppendLine($"  Gioi tinh: {p.Gender}");
                if (!string.IsNullOrEmpty(p.BrandName)) sb.AppendLine($"  Thuong hieu: {p.BrandName}");
                if (!string.IsNullOrEmpty(p.Material)) sb.AppendLine($"  Chat lieu: {p.Material}");
                if (!string.IsNullOrEmpty(p.Description)) sb.AppendLine($"  Mo ta: {p.Description}");
                if (p.Tags != null && p.Tags.Any()) sb.AppendLine($"  Tags: {string.Join(", ", p.Tags)}");

                // Rating & Sales
                if (p.AverageRating.HasValue && p.TotalReviews.HasValue && p.TotalReviews > 0)
                    sb.AppendLine($"  Danh gia: {p.AverageRating:F1}/5 ({p.TotalReviews} luot)");
                if (p.SoldCount.HasValue && p.SoldCount > 0)
                    sb.AppendLine($"  Da ban: {p.SoldCount} san pham");

                // Available variants (color + size + stock)
                var inStockVariants = p.ProductVariants.Where(v => (v.StockQuantity ?? 0) > 0).ToList();
                if (inStockVariants.Any())
                {
                    var colorGroups = inStockVariants.GroupBy(v => v.Color);
                    foreach (var cg in colorGroups)
                    {
                        var sizes = string.Join(", ", cg.Select(v => $"{v.Size}(con {v.StockQuantity})"));
                        sb.AppendLine($"  Mau {cg.Key}: {sizes}");
                    }
                }

                // Size guide details
                if (p.SizeGuides.Any())
                {
                    sb.AppendLine($"  Bang size:");
                    foreach (var sg in p.SizeGuides.OrderBy(s => s.SizeName))
                    {
                        var rangeParts = new List<string>();
                        if (sg.MinWeight.HasValue && sg.MaxWeight.HasValue)
                            rangeParts.Add($"Nang {sg.MinWeight}-{sg.MaxWeight}kg");
                        if (sg.MinBust.HasValue && sg.MaxBust.HasValue)
                            rangeParts.Add($"Nguc {sg.MinBust}-{sg.MaxBust}cm");
                        if (sg.MinWaist.HasValue && sg.MaxWaist.HasValue)
                            rangeParts.Add($"Eo {sg.MinWaist}-{sg.MaxWaist}cm");
                        if (sg.MinHips.HasValue && sg.MaxHips.HasValue)
                            rangeParts.Add($"Hong {sg.MinHips}-{sg.MaxHips}cm");

                        var exactParts = new List<string>();
                        if (sg.ChestCm.HasValue) exactParts.Add($"Nguc {sg.ChestCm}cm");
                        if (sg.WaistCm.HasValue) exactParts.Add($"Eo {sg.WaistCm}cm");
                        if (sg.HipCm.HasValue) exactParts.Add($"Hong {sg.HipCm}cm");
                        if (sg.ShoulderCm.HasValue) exactParts.Add($"Vai {sg.ShoulderCm}cm");
                        if (sg.LengthCm.HasValue) exactParts.Add($"Dai {sg.LengthCm}cm");
                        if (sg.SleeveCm.HasValue) exactParts.Add($"Tay {sg.SleeveCm}cm");

                        var rangeInfo = rangeParts.Any() ? string.Join(", ", rangeParts) : "";
                        var exactInfo = exactParts.Any() ? $" | Do: {string.Join(", ", exactParts)}" : "";

                        sb.AppendLine($"    Size {sg.SizeName}: {rangeInfo}{exactInfo}");
                    }
                }

                sb.AppendLine();
            }

            // === REVIEW INSIGHTS ===
            if (reviewInsights.Any())
            {
                sb.AppendLine("=== THAM KHAO TU KHACH HANG DA MUA ===");
                foreach (var r in reviewInsights)
                {
                    var productName = products.FirstOrDefault(p => p.Id == r.ProductId)?.Name ?? "";
                    sb.AppendLine($"- {productName} | Size {r.SizeOrdered} | Khach {r.HeightCm}cm/{r.WeightKg}kg | {r.Rating}/5 sao");
                    if (!string.IsNullOrEmpty(r.Comment))
                    {
                        var shortComment = r.Comment.Length > 80 ? r.Comment[..80] + "..." : r.Comment;
                        sb.AppendLine($"  Nhan xet: {shortComment}");
                    }
                }
                sb.AppendLine();
            }

            // === SIZING TIPS ===
            sb.AppendLine("=== HUONG DAN TU VAN SIZE ===");
            sb.AppendLine("1. So sanh so do khach hang voi BANG SIZE CU THE cua tung san pham.");
            sb.AppendLine("2. Neu khach o giua 2 size, khuyen chon size LON hon de thoai mai.");
            sb.AppendLine("3. Voi khach thich mac rong (Loose Fit), khuyen len 1 size.");
            sb.AppendLine("4. Voi khach thich mac om (Slim Fit), chon size vua khit so do.");
            sb.AppendLine("5. Tham khao review cua khach da mua co so do tuong tu de tu van chinh xac hon.");
            sb.AppendLine("6. Luon ghi chu: 'So do mang tinh tham khao, co the chenh lech 1-2cm tuy dang nguoi'.");

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

            var jsonContent = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(url, httpContent);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new Exception($"Gemini API error: {response.StatusCode} - {errorBody}");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseBody);

            // Extract text from Gemini response
            var candidates = doc.RootElement.GetProperty("candidates");
            if (candidates.GetArrayLength() > 0)
            {
                var content = candidates[0].GetProperty("content");
                var parts = content.GetProperty("parts");
                if (parts.GetArrayLength() > 0)
                {
                    return parts[0].GetProperty("text").GetString() ?? "No response from AI.";
                }
            }

            return "No response from AI.";
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
