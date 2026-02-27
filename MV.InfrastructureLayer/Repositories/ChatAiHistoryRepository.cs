using Microsoft.EntityFrameworkCore;
using MV.DomainLayer.Entities;
using MV.InfrastructureLayer.DBContext;
using MV.InfrastructureLayer.Interfaces;

namespace MV.InfrastructureLayer.Repositories
{
    public class ChatAiHistoryRepository : IChatAiHistoryRepository
    {
        private readonly FashionDbContext _context;

        public ChatAiHistoryRepository(FashionDbContext context)
        {
            _context = context;
        }

        public async Task<ChatAiHistory> CreateAsync(ChatAiHistory chatHistory)
        {
            _context.ChatAiHistories.Add(chatHistory);
            await _context.SaveChangesAsync();
            return chatHistory;
        }

        public async Task CreateRangeAsync(List<ChatAiHistory> chatHistories)
        {
            _context.ChatAiHistories.AddRange(chatHistories);
            await _context.SaveChangesAsync();
        }

        public async Task<List<ChatAiHistory>> GetBySessionIdAsync(string sessionId, int userId)
        {
            return await _context.ChatAiHistories
                .Where(c => c.SessionId == sessionId && c.UserId == userId)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<(string SessionId, string? LastMessage, int MessageCount, DateTime? CreatedAt, DateTime? UpdatedAt)>> GetSessionsByUserIdAsync(int userId)
        {
            var sessions = await _context.ChatAiHistories
                .Where(c => c.UserId == userId && c.SessionId != null)
                .GroupBy(c => c.SessionId!)
                .Select(g => new
                {
                    SessionId = g.Key,
                    LastMessage = g.OrderByDescending(c => c.CreatedAt).First().Content,
                    MessageCount = g.Count(),
                    CreatedAt = g.Min(c => c.CreatedAt),
                    UpdatedAt = g.Max(c => c.CreatedAt)
                })
                .OrderByDescending(s => s.UpdatedAt)
                .ToListAsync();

            return sessions.Select(s => (s.SessionId, (string?)s.LastMessage, s.MessageCount, s.CreatedAt, s.UpdatedAt)).ToList();
        }

        public async Task<bool> SessionBelongsToUserAsync(string sessionId, int userId)
        {
            return await _context.ChatAiHistories
                .AnyAsync(c => c.SessionId == sessionId && c.UserId == userId);
        }

        public async Task<int> DeleteBySessionIdAndUserIdAsync(string sessionId, int userId)
        {
            return await _context.ChatAiHistories
                .Where(c => c.SessionId == sessionId && c.UserId == userId)
                .ExecuteDeleteAsync();
        }
    }
}
