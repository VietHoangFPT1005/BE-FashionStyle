namespace MV.DomainLayer.DTOs.Chat.Response
{
    public class ChatSessionListResponse
    {
        public string SessionId { get; set; } = string.Empty;
        public string? LastMessage { get; set; }
        public int MessageCount { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
