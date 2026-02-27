using Microsoft.EntityFrameworkCore;
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

        public ChatAiService(
            FashionDbContext context,
            IChatAiHistoryRepository chatRepository,
            IOptions<GeminiSettings> geminiSettings,
            IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _chatRepository = chatRepository;
            _geminiSettings = geminiSettings.Value;
            _httpClientFactory = httpClientFactory;
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

            // 4. Query suitable products with size guides
            var productsWithGuides = await _context.Products
                .Include(p => p.SizeGuides)
                .Include(p => p.ProductImages)
                .Include(p => p.ProductVariants)
                .Where(p => p.IsActive == true && p.IsDeleted != true)
                .Where(p => p.SizeGuides.Any())
                .Where(p => p.ProductVariants.Any(v => (v.StockQuantity ?? 0) > 0))
                .Take(20)
                .ToListAsync();

            // 5. Build AI prompt
            var systemPrompt = BuildSystemPrompt(bodyProfile, productsWithGuides);

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
                aiResponse = $"I'm sorry, I'm unable to process your request at the moment. Please try again later. (Error: {ex.Message})";
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

        private string BuildSystemPrompt(UserBodyProfile? bodyProfile, List<Product> products)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Ban la chuyen gia tu van thoi trang Big Size tai Viet Nam.");
            sb.AppendLine("Nhiem vu: Tu van chon san pham va size phu hop voi so do khach hang.");
            sb.AppendLine("Phong cach: Than thien, tu tin, khong dung tu miet thi ngoai co.");
            sb.AppendLine("Luon tra loi bang tieng Viet.");
            sb.AppendLine("Khi goi y san pham, hay de cap ten san pham CHINH XAC nhu trong danh sach.");
            sb.AppendLine();

            if (bodyProfile != null)
            {
                sb.AppendLine("THONG TIN KHACH HANG:");
                if (bodyProfile.Height.HasValue) sb.AppendLine($"- Chieu cao: {bodyProfile.Height}cm");
                if (bodyProfile.Weight.HasValue) sb.AppendLine($"- Can nang: {bodyProfile.Weight}kg");
                if (bodyProfile.Bust.HasValue) sb.AppendLine($"- Vong nguc: {bodyProfile.Bust}cm");
                if (bodyProfile.Waist.HasValue) sb.AppendLine($"- Vong eo: {bodyProfile.Waist}cm");
                if (bodyProfile.Hips.HasValue) sb.AppendLine($"- Vong hong: {bodyProfile.Hips}cm");
                if (!string.IsNullOrEmpty(bodyProfile.BodyShape)) sb.AppendLine($"- Dang nguoi: {bodyProfile.BodyShape}");
                if (!string.IsNullOrEmpty(bodyProfile.FitPreference)) sb.AppendLine($"- So thich: {bodyProfile.FitPreference}");
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("KHACH HANG CHUA CO SO DO CO THE. Hay hoi khach hang ve chieu cao, can nang de tu van tot hon.");
                sb.AppendLine();
            }

            sb.AppendLine("SAN PHAM CO SAN (co size phu hop):");
            foreach (var p in products.Take(15))
            {
                var price = p.SalePrice.HasValue ? $"{p.SalePrice:N0}d (goc {p.Price:N0}d)" : $"{p.Price:N0}d";
                var sizes = string.Join(", ", p.SizeGuides.Select(sg => sg.SizeName));
                sb.AppendLine($"- [ID:{p.Id}] {p.Name} | Gia: {price} | Size: {sizes}");
            }

            return sb.ToString();
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
