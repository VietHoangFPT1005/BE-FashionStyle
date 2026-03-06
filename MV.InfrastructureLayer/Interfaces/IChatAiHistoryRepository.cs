using MV.DomainLayer.Entities;

namespace MV.InfrastructureLayer.Interfaces
{
    public interface IChatAiHistoryRepository
    {
        Task<ChatAiHistory> CreateAsync(ChatAiHistory chatHistory);
        Task CreateRangeAsync(List<ChatAiHistory> chatHistories);
        Task<List<ChatAiHistory>> GetBySessionIdAsync(string sessionId, int userId);
        Task<List<(string SessionId, string? LastMessage, int MessageCount, DateTime? CreatedAt, DateTime? UpdatedAt)>> GetSessionsByUserIdAsync(int userId);
        Task<bool> SessionBelongsToUserAsync(string sessionId, int userId);
        Task<int> DeleteBySessionIdAndUserIdAsync(string sessionId, int userId);
    }
}
