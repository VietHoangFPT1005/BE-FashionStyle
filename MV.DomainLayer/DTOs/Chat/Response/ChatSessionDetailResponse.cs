namespace MV.DomainLayer.DTOs.Chat.Response
{
    public class ChatSessionDetailResponse
    {
        public string SessionId { get; set; } = string.Empty;
        public List<ChatHistoryMessage> Messages { get; set; } = new();
    }

    public class ChatHistoryMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public List<int>? SuggestedProductIds { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
