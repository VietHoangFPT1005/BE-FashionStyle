// ================================================================
// [CHAT SUPPORT - MỚI THÊM]
// DTO response cho tính năng chat hỗ trợ Customer <-> Staff/Admin
// ================================================================

namespace MV.DomainLayer.DTOs.Chat.Response;

/// <summary>
/// [CHAT SUPPORT - MỚI THÊM]
/// Một tin nhắn trong cuộc hội thoại hỗ trợ
/// </summary>
public class SupportMessageDto
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public int SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string? SenderAvatar { get; set; }
    public int SenderRole { get; set; }       // 1=Admin, 2=Staff, 3=Customer
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// [CHAT SUPPORT - MỚI THÊM]
/// Cuộc hội thoại của một khách hàng (dành cho Staff/Admin xem danh sách)
/// </summary>
public class SupportConversationDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerAvatar { get; set; }
    public string LastMessage { get; set; } = string.Empty;
    public DateTime LastMessageAt { get; set; }
    public int UnreadCount { get; set; }      // Số tin nhắn Customer gửi chưa đọc
}
