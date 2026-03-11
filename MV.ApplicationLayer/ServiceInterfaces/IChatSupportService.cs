// ================================================================
// [CHAT SUPPORT - MỚI THÊM]
// Interface Service cho chat hỗ trợ Customer <-> Staff/Admin
// ================================================================

using MV.DomainLayer.DTOs.Chat.Response;

namespace MV.ApplicationLayer.ServiceInterfaces;

/// <summary>
/// [CHAT SUPPORT - MỚI THÊM]
/// Business logic cho tính năng chat hỗ trợ
/// </summary>
public interface IChatSupportService
{
    // Lấy lịch sử chat của customer (Customer tự xem hoặc Staff xem)
    Task<List<SupportMessageDto>> GetHistoryAsync(int customerId, int skip = 0, int take = 50);

    // Staff/Admin lấy danh sách tất cả cuộc hội thoại
    Task<List<SupportConversationDto>> GetConversationsAsync();

    // Lưu tin nhắn mới (dùng trong Hub sau khi nhận được message)
    Task<SupportMessageDto> SaveMessageAsync(int customerId, int senderId, int senderRole, string message);

    // Đánh dấu Staff đã đọc tin nhắn của customer
    Task MarkAsReadAsync(int customerId);
}
