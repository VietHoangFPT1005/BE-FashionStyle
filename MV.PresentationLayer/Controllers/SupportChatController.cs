// ================================================================
// [CHAT SUPPORT]
// REST Controller cho chat hỗ trợ
//
// Endpoints:
//   GET  /api/SupportChat/history              → Customer lấy lịch sử chat của mình
//   GET  /api/SupportChat/conversations        → Staff/Admin lấy danh sách hội thoại
//   GET  /api/SupportChat/history/{customerId} → Staff/Admin lấy lịch sử chat của customer
//   PUT  /api/SupportChat/read/{customerId}    → Staff/Admin đánh dấu đã đọc
//   POST /api/SupportChat/upload-image         → Upload ảnh chat lên Cloudinary
// ================================================================

using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MV.ApplicationLayer.Interfaces;
using MV.DomainLayer.DTOs.Common;
using System.Security.Claims;

namespace MV.PresentationLayer.Controllers;

/// <summary>
/// [CHAT SUPPORT - MỚI THÊM]
/// REST API để load lịch sử chat (trước khi SignalR kết nối)
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SupportChatController : ControllerBase
{
    private readonly IChatSupportService _chatService;
    private readonly IConfiguration _config;

    public SupportChatController(IChatSupportService chatService, IConfiguration config)
    {
        _chatService = chatService;
        _config      = config;
    }

    // ── Customer: lấy lịch sử chat của chính mình ─────────────

    /// <summary>
    /// Customer gọi để load lịch sử chat trước khi kết nối SignalR
    /// GET /api/SupportChat/history?skip=0&take=50
    /// </summary>
    [HttpGet("history")]
    [Authorize(Roles = "3")]   // chỉ Customer
    public async Task<IActionResult> GetMyHistory([FromQuery] int skip = 0, [FromQuery] int take = 50)
    {
        var userId = GetUserId();
        var history = await _chatService.GetHistoryAsync(userId, skip, take);
        return Ok(ApiResponse.SuccessResponse(history));
    }

    // ── Staff/Admin: quản lý hội thoại ────────────────────────

    /// <summary>
    /// Staff/Admin lấy danh sách tất cả cuộc hội thoại với customers
    /// GET /api/SupportChat/conversations
    /// </summary>
    [HttpGet("conversations")]
    [Authorize(Roles = "1,2")]  // Admin=1, Staff=2
    public async Task<IActionResult> GetConversations()
    {
        var conversations = await _chatService.GetConversationsAsync();
        return Ok(ApiResponse.SuccessResponse(conversations));
    }

    /// <summary>
    /// Staff/Admin lấy lịch sử chat của customer cụ thể
    /// GET /api/SupportChat/history/{customerId}?skip=0&take=50
    /// </summary>
    [HttpGet("history/{customerId:int}")]
    [Authorize(Roles = "1,2")]
    public async Task<IActionResult> GetCustomerHistory(int customerId, [FromQuery] int skip = 0, [FromQuery] int take = 50)
    {
        var history = await _chatService.GetHistoryAsync(customerId, skip, take);
        return Ok(ApiResponse.SuccessResponse(history));
    }

    /// <summary>
    /// Staff/Admin đánh dấu đã đọc tin nhắn của customer
    /// PUT /api/SupportChat/read/{customerId}
    /// </summary>
    [HttpPut("read/{customerId:int}")]
    [Authorize(Roles = "1,2")]
    public async Task<IActionResult> MarkAsRead(int customerId)
    {
        await _chatService.MarkAsReadAsync(customerId);
        return Ok(ApiResponse.SuccessResponse("Đã đánh dấu đã đọc"));
    }

    // ── Upload hình ảnh ───────────────────────────────────────

    /// <summary>
    /// Upload ảnh chat lên Cloudinary, trả về secure URL
    /// POST /api/SupportChat/upload-image  (multipart/form-data, field name: file)
    /// </summary>
    [HttpPost("upload-image")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadChatImage([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse.ErrorResponse("Không có file được gửi lên."));

        var cloudName = _config["CloudinarySettings:CloudName"];
        var apiKey    = _config["CloudinarySettings:ApiKey"];
        var apiSecret = _config["CloudinarySettings:ApiSecret"];

        var account    = new Account(cloudName, apiKey, apiSecret);
        var cloudinary = new Cloudinary(account);

        using var stream = file.OpenReadStream();
        var uploadParams = new ImageUploadParams
        {
            File       = new FileDescription(file.FileName, stream),
            Folder     = "chat-images",
            UseFilename = false,
        };

        var result = await cloudinary.UploadAsync(uploadParams);
        if (result.Error != null)
            return BadRequest(ApiResponse.ErrorResponse(result.Error.Message));

        return Ok(ApiResponse.SuccessResponse(result.SecureUrl.ToString()));
    }

    // ── Helper ────────────────────────────────────────────────

    private int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                 ?? User.FindFirst("sub");
        return int.TryParse(claim?.Value, out var id) ? id : 0;
    }
}
