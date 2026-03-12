// ================================================================
// [CHAT SUPPORT - MỚI THÊM]
// DTO request cho tính năng chat hỗ trợ Customer <-> Staff/Admin
// ================================================================

namespace MV.DomainLayer.DTOs.Chat.Request;

/// <summary>
/// [CHAT SUPPORT - MỚI THÊM]
/// Staff/Admin gửi tin nhắn cho khách hàng cụ thể (qua REST nếu cần)
/// </summary>
public class StaffSendMessageRequest
{
    public int CustomerId { get; set; }   // ID khách hàng cần trả lời
    public string Message { get; set; } = string.Empty;
}
