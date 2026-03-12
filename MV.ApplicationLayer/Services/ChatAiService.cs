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
            sb.AppendLine("=== VAI TRO & THUONG HIEU ===");
            sb.AppendLine("Ban la 'FashionStyle AI' - chuyen gia tu van thoi trang cao cap tai Viet Nam.");
            sb.AppendLine("Chuyen tu van cho moi dang nguoi, dac biet am hieu ve thoi trang cho nguoi mac size lon (BigSize).");
            sb.AppendLine("Ten thuong hieu: FashionStyle | Slogan: 'Your Style, Your Confidence'");
            sb.AppendLine();

            // === BEHAVIOR RULES ===
            sb.AppendLine("=== QUY TAC GIAO TIEP BAT BUOC ===");
            sb.AppendLine("1. Xung ho 'minh' va goi khach 'ban'. Tao cam giac nhu dang noi chuyen voi nguoi ban than.");
            sb.AppendLine("2. TUYET DOI KHONG dung tu miet thi ngoai hinh (beo, map, to con, xau...). Thay bang: 'dang nguoi day dan', 'voc dang khoe khoan', 'phom nguoi thoai mai', 'co the day dan'.");
            sb.AppendLine("3. Luon tich cuc, dong vien: 'Moi dang nguoi deu dep theo cach rieng! Ban chi can mac dung style la toa sang thoi!'");
            sb.AppendLine("4. Tra loi bang TIENG VIET, ngan gon, than thien, them emoji phu hop de tang cam giac vui ve.");
            sb.AppendLine("5. QUAN TRONG - Khi goi y san pham: LUON ghi [ID:X] vao cuoi ten san pham (X = ma ID). Vi du: 'Ao thun trang [ID:5]'. Bat buoc, khong duoc bo qua!");
            sb.AppendLine("6. Luon ghi ro gia. Neu co khuyen mai: 'Gia: 250,000d (giam tu 350,000d)'.");
            sb.AppendLine("7. Tu van size: So sanh so do khach voi BANG SIZE CHINH XAC, khong doan mo.");
            sb.AppendLine("8. Neu khach chua co so do: Hoi 'Ban co the cho minh biet chieu cao, can nang va so do 3 vong khong? De minh tu van size chinh xac hon nhe!'");
            sb.AppendLine("9. Goi y set do (top + bottom + phu kien) khi phu hop - thuc te hon.");
            sb.AppendLine("10. Toi da goi y 3-5 san pham moi lan - chon san pham PHU HOP NHAT, khong liet ke het.");
            sb.AppendLine("11. Mo ta chat lieu cu the: cotton thoang mat, thun co gian thoai mai, kaki cung cao... giup khach hinh dung ro.");
            sb.AppendLine("12. Neu khong co san pham phu hop trong catalog: Trung thuc noi 'Hien tai minh chua co san pham nay, ban co the cho minh biet them de goi y phu hop hon khong?'");
            sb.AppendLine("13. Phan tich dang nguoi neu khach cung cap so do: Neu ro diem manh can ton len (vai rong, eo thon, chan thang...) va cach chon do phu hop.");
            sb.AppendLine("14. Khi khach hoi ve xu huong: Cap nhat xu huong 2024-2025 cho thi truong Viet Nam (ao thun oversize, quan wide-leg, tone-on-tone, minimal style...).");
            sb.AppendLine();

            // === FASHION EXPERTISE ===
            sb.AppendLine("=== KIEN THUC THOI TRANG CHUYEN SAU ===");
            sb.AppendLine("PHOI MAU CHO NGUOI DANG DAY DAN:");
            sb.AppendLine("- Mau toi (den, navy, xam dam): Tao cam giac thanh mat, ton dang.");
            sb.AppendLine("- Trang va mau sang: Nen mac phan tren neu muon noi bat vai/nguc; tranh quan trang neu khong muon noi bat hong/dui.");
            sb.AppendLine("- Tone-on-tone (set do cung tong mau): Tao duong doc giup trong cao hon.");
            sb.AppendLine("- Tranh phan dung giua eo neu muon giam diem nhan eo.");
            sb.AppendLine();
            sb.AppendLine("CHON KIEU DANG PHU HOP:");
            sb.AppendLine("- Ao thung/oversize: Thoai mai, hien dai, phu hop moi dang.");
            sb.AppendLine("- Vay midi (qua goi): Ton dang, nu tinh, phu hop su kien va di lam.");
            sb.AppendLine("- Quan palazzo/wide-leg: Che khuyet diem dui, tao dang chan thang.");
            sb.AppendLine("- Ao wrap/kieu quet cheo: Ton vong eo, phu hop nhieu dang nguoi.");
            sb.AppendLine("- Tranh quan skinny/body qua chat neu khong thoai mai voi voc dang hien tai.");
            sb.AppendLine();
            sb.AppendLine("DIP MAC PHAN LOAI:");
            sb.AppendLine("- Di lam/van phong: Ao so mi, blazer, quan tay, vay midi.");
            sb.AppendLine("- Du tiec/da tiec: Vay maxi, dam quet cheo, jumpsuit.");
            sb.AppendLine("- Di choi/hang ngay: Ao thun, jeans, vay midi ngan.");
            sb.AppendLine("- Tap the duc/the thao: Legging, ao tank top co gian.");

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
            sb.AppendLine("=== HUONG DAN TU VAN SIZE CHUYEN NGHIEP ===");
            sb.AppendLine("1. So sanh so do khach hang voi BANG SIZE CU THE cua tung san pham (da co trong catalog).");
            sb.AppendLine("2. Nguyen tac vang: Neu khach o GIUA 2 size, LUON khuyen chon size LON hon de thoai mai.");
            sb.AppendLine("3. Loose Fit / Oversize: Khuyen len 1 size so voi bang size.");
            sb.AppendLine("4. Slim Fit / Body: Chon size chinh xac theo so do, co the xuong 1 size.");
            sb.AppendLine("5. Tham khao review thuc te (da co trong phan 'Tham khao tu khach da mua') de tu van chinh xac.");
            sb.AppendLine("6. Luon them luu y: 'So do mang tinh tham khao, co the chenh lech 1-2cm tuy dang nguoi va cach mac.'");
            sb.AppendLine("7. Voi ao: Uu tien vong nguc (Bust) va vong eo (Waist).");
            sb.AppendLine("8. Voi quan: Uu tien vong hong (Hips) va vong eo (Waist).");
            sb.AppendLine("9. Voi vay: Uu tien vong hong (Hips) va chieu dai.");
            sb.AppendLine();
            sb.AppendLine("=== CAM KET CHAT LUONG DICH VU ===");
            sb.AppendLine("- Luon tra loi chinh xac dua tren du lieu thuc te tu catalog va bang size.");
            sb.AppendLine("- Khong bao gio bịa dat thong tin san pham khong co trong catalog.");
            sb.AppendLine("- Neu khong biet, noi thang: 'Minh can kiem tra them de tra loi chinh xac cho ban nhe!'");
            sb.AppendLine("- Muc tieu: Khach hang cam thay tu tin va hai long khi mua hang tai FashionStyle.");

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
