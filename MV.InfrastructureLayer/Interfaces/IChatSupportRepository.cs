// ================================================================
// [CHAT SUPPORT - MỚI THÊM]
// Interface Repository cho ChatSupportMessages
// ================================================================

using MV.DomainLayer.Entities;

namespace MV.InfrastructureLayer.Interfaces;

/// <summary>
/// [CHAT SUPPORT - MỚI THÊM]
/// Repository để truy xuất dữ liệu bảng ChatSupportMessages
/// </summary>
public interface IChatSupportRepository
{
    // Lấy toàn bộ lịch sử chat của một customer (Customer hoặc Staff xem)
    Task<List<ChatSupportMessage>> GetHistoryAsync(int customerId, int skip = 0, int take = 50);

    // Lấy danh sách các cuộc hội thoại (dành cho Staff/Admin - mỗi customer 1 entry)
    Task<List<ChatSupportMessage>> GetLatestMessagePerCustomerAsync();

    // Đếm tin nhắn chưa đọc của một customer (Staff gửi cho customer chưa đọc)
    Task<int> GetUnreadCountAsync(int customerId);

    // Lưu tin nhắn mới
    Task<ChatSupportMessage> AddAsync(ChatSupportMessage message);

    // Đánh dấu đã đọc: Staff đọc tin nhắn của customer
    Task MarkAsReadAsync(int customerId);
}
