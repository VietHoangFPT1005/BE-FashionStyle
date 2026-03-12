// ================================================================
// [CHAT SUPPORT - MỚI THÊM]
// Service implementation cho chat hỗ trợ Customer <-> Staff/Admin
// ================================================================

using MV.ApplicationLayer.Interfaces;
using MV.DomainLayer.DTOs.Chat.Response;
using MV.DomainLayer.Entities;
using MV.InfrastructureLayer.Interfaces;

namespace MV.ApplicationLayer.Services;

/// <summary>
/// [CHAT SUPPORT - MỚI THÊM]
/// Xử lý business logic: lưu tin nhắn, lấy lịch sử, danh sách hội thoại
/// </summary>
public class ChatSupportService : IChatSupportService
{
    private readonly IChatSupportRepository _repo;

    public ChatSupportService(IChatSupportRepository repo)
    {
        _repo = repo;
    }

    // Lấy lịch sử chat và map sang DTO
    public async Task<List<SupportMessageDto>> GetHistoryAsync(int customerId, int skip = 0, int take = 50)
    {
        var messages = await _repo.GetHistoryAsync(customerId, skip, take);
        return messages.Select(MapToDto).ToList();
    }

    // Lấy danh sách cuộc hội thoại cho Staff/Admin
    public async Task<List<SupportConversationDto>> GetConversationsAsync()
    {
        var latestMessages = await _repo.GetLatestMessagePerCustomerAsync();

        var tasks = latestMessages.Select(async m => new SupportConversationDto
        {
            CustomerId    = m.CustomerId,
            CustomerName  = m.Customer?.FullName ?? m.Customer?.Username ?? "Khách hàng",
            CustomerAvatar = m.Customer?.AvatarUrl,
            LastMessage   = m.Message,
            LastMessageAt = m.CreatedAt,
            UnreadCount   = await _repo.GetUnreadCountAsync(m.CustomerId)
        });

        return (await Task.WhenAll(tasks)).ToList();
    }

    // Lưu tin nhắn mới vào DB và trả về DTO
    public async Task<SupportMessageDto> SaveMessageAsync(int customerId, int senderId, int senderRole, string message)
    {
        var entity = new ChatSupportMessage
        {
            CustomerId = customerId,
            SenderId   = senderId,
            SenderRole = senderRole,
            Message    = message,
            IsRead     = false,
        };

        var saved = await _repo.AddAsync(entity);

        // Load navigation property Sender để có tên người gửi
        saved.Sender = saved.Sender; // đã include trong repo
        return MapToDto(saved);
    }

    // Staff đọc tin → đánh dấu đã đọc
    public async Task MarkAsReadAsync(int customerId)
    {
        await _repo.MarkAsReadAsync(customerId);
    }

    // Helper: Map entity → DTO
    private static SupportMessageDto MapToDto(ChatSupportMessage m) => new()
    {
        Id           = m.Id,
        CustomerId   = m.CustomerId,
        SenderId     = m.SenderId,
        SenderName   = m.Sender?.FullName ?? m.Sender?.Username ?? "Người dùng",
        SenderAvatar = m.Sender?.AvatarUrl,
        SenderRole   = m.SenderRole,
        Message      = m.Message,
        IsRead       = m.IsRead,
        CreatedAt    = m.CreatedAt,
    };
}
