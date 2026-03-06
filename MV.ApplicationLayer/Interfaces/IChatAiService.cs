using MV.DomainLayer.DTOs.Chat.Request;
using MV.DomainLayer.DTOs.Chat.Response;
using MV.DomainLayer.DTOs.Common;

namespace MV.ApplicationLayer.ServiceInterfaces
{
    public interface IChatAiService
    {
        Task<ApiResponse<ChatMessageResponse>> SendMessageAsync(int userId, ChatMessageRequest request);
        Task<ApiResponse<List<ChatSessionListResponse>>> GetSessionsAsync(int userId);
        Task<ApiResponse<ChatSessionDetailResponse>> GetSessionHistoryAsync(int userId, string sessionId);
        Task<ApiResponse<object>> DeleteSessionAsync(int userId, string sessionId);
    }
}
