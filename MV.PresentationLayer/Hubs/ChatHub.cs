// ================================================================
// [CHAT SUPPORT - MỚI THÊM]
// SignalR Hub cho chat real-time Customer <-> Staff/Admin
//
// Flow:
//   Customer kết nối  → join group "customer_{userId}"
//   Staff/Admin kết nối → join group "staff"
//   Customer gửi tin  → lưu DB + push đến group "staff"
//   Staff gửi tin     → lưu DB + push đến group "customer_{customerId}"
// ================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MV.ApplicationLayer.ServiceInterfaces;
using System.Security.Claims;

namespace MV.PresentationLayer.Hubs;

/// <summary>
/// [CHAT SUPPORT - MỚI THÊM]
/// SignalR Hub - yêu cầu JWT hợp lệ mới kết nối được
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    private readonly IChatSupportService _chatService;

    public ChatHub(IChatSupportService chatService)
    {
        _chatService = chatService;
    }

    // ── Kết nối ──────────────────────────────────────────────────

    public override async Task OnConnectedAsync()
    {
        var userId   = GetUserId();
        var userRole = GetUserRole();

        if (userRole == 3)
        {
            // Customer → join group riêng để nhận phản hồi từ Staff
            await Groups.AddToGroupAsync(Context.ConnectionId, $"customer_{userId}");
        }
        else if (userRole == 1 || userRole == 2)
        {
            // Admin (1) hoặc Staff (2) → join group "staff" để nhận tin từ mọi customer
            await Groups.AddToGroupAsync(Context.ConnectionId, "staff");
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }

    // ── Methods gọi từ Client ─────────────────────────────────────

    /// <summary>
    /// [CHAT SUPPORT - MỚI THÊM]
    /// Customer gọi method này để gửi tin nhắn đến Staff/Admin
    /// </summary>
    public async Task SendMessageToSupport(string message)
    {
        var customerId = GetUserId();   // Customer chính là người gửi
        var senderId   = customerId;

        // Lưu vào DB
        var dto = await _chatService.SaveMessageAsync(
            customerId: customerId,
            senderId:   senderId,
            senderRole: 3,          // 3 = Customer
            message:    message
        );

        // Gửi về chính customer để hiển thị tin mình vừa gửi
        await Clients.Group($"customer_{customerId}")
            .SendAsync("ReceiveMessage", dto);

        // Gửi đến tất cả Staff/Admin đang online
        await Clients.Group("staff")
            .SendAsync("ReceiveMessage", dto);
    }

    /// <summary>
    /// [CHAT SUPPORT - MỚI THÊM]
    /// Staff/Admin gọi method này để trả lời khách hàng
    /// </summary>
    public async Task SendMessageToCustomer(int customerId, string message)
    {
        var senderId   = GetUserId();
        var senderRole = GetUserRole();

        // Chỉ Staff (2) và Admin (1) mới được gọi method này
        if (senderRole != 1 && senderRole != 2) return;

        // Lưu vào DB
        var dto = await _chatService.SaveMessageAsync(
            customerId: customerId,
            senderId:   senderId,
            senderRole: senderRole,
            message:    message
        );

        // Đẩy đến customer
        await Clients.Group($"customer_{customerId}")
            .SendAsync("ReceiveMessage", dto);

        // Đẩy lại chính Staff đang gửi (để hiện trong UI của họ)
        await Clients.Caller.SendAsync("ReceiveMessage", dto);
    }

    // ── Helpers ──────────────────────────────────────────────────

    // Lấy UserId từ JWT claim
    private int GetUserId()
    {
        var claim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)
                 ?? Context.User?.FindFirst("sub");
        return int.TryParse(claim?.Value, out var id) ? id : 0;
    }

    // Lấy Role từ JWT claim (1=Admin, 2=Staff, 3=Customer, 4=Shipper)
    private int GetUserRole()
    {
        var claim = Context.User?.FindFirst(ClaimTypes.Role)
                 ?? Context.User?.FindFirst("role");
        return int.TryParse(claim?.Value, out var role) ? role : 0;
    }
}
