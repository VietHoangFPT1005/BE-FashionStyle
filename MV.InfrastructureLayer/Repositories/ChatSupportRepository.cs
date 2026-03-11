// ================================================================
// [CHAT SUPPORT - MỚI THÊM]
// Repository implementation cho ChatSupportMessages
// ================================================================

using Microsoft.EntityFrameworkCore;
using MV.DomainLayer.Entities;
using MV.InfrastructureLayer.DBContext;
using MV.InfrastructureLayer.Interfaces;

namespace MV.InfrastructureLayer.Repositories;

/// <summary>
/// [CHAT SUPPORT - MỚI THÊM]
/// Truy xuất dữ liệu ChatSupportMessages từ PostgreSQL
/// </summary>
public class ChatSupportRepository : IChatSupportRepository
{
    private readonly FashionDbContext _db;

    public ChatSupportRepository(FashionDbContext db)
    {
        _db = db;
    }

    // Lấy lịch sử chat của customer, sắp xếp cũ → mới, phân trang
    public async Task<List<ChatSupportMessage>> GetHistoryAsync(int customerId, int skip = 0, int take = 50)
    {
        // Include phải đặt trước Where/OrderBy theo convention EF Core
        return await _db.ChatSupportMessages
            .Include(m => m.Sender) // Join để lấy tên người gửi
            .Where(m => m.CustomerId == customerId)
            .OrderBy(m => m.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    // Lấy tin nhắn mới nhất của mỗi customer (Staff xem danh sách hội thoại)
    // Dùng 2 bước vì EF Core không hỗ trợ Include() sau GroupBy().Select()
    public async Task<List<ChatSupportMessage>> GetLatestMessagePerCustomerAsync()
    {
        // Bước 1: lấy Id của tin nhắn mới nhất của mỗi customer
        var latestIds = await _db.ChatSupportMessages
            .GroupBy(m => m.CustomerId)
            .Select(g => g.Max(m => m.Id))
            .ToListAsync();

        // Bước 2: load đầy đủ kèm navigation property Customer
        return await _db.ChatSupportMessages
            .Include(m => m.Customer)
            .Where(m => latestIds.Contains(m.Id))
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
    }

    // Đếm số tin nhắn Customer gửi mà Staff chưa đọc
    public async Task<int> GetUnreadCountAsync(int customerId)
    {
        return await _db.ChatSupportMessages
            .CountAsync(m => m.CustomerId == customerId
                          && m.SenderRole == 3       // 3 = Customer
                          && m.IsRead == false);
    }

    // Lưu tin nhắn mới vào DB
    public async Task<ChatSupportMessage> AddAsync(ChatSupportMessage message)
    {
        message.CreatedAt = DateTime.Now;
        _db.ChatSupportMessages.Add(message);
        await _db.SaveChangesAsync();
        return message;
    }

    // Staff đọc tin nhắn → đánh dấu tất cả tin của customer là đã đọc
    public async Task MarkAsReadAsync(int customerId)
    {
        await _db.ChatSupportMessages
            .Where(m => m.CustomerId == customerId
                     && m.SenderRole == 3     // chỉ mark tin của Customer
                     && m.IsRead == false)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true));
    }
}
