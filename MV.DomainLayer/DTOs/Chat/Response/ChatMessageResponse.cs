namespace MV.DomainLayer.DTOs.Chat.Response
{
    public class ChatMessageResponse
    {
        public string SessionId { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public List<ChatSuggestedProduct>? SuggestedProducts { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class ChatSuggestedProduct
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal? SalePrice { get; set; }
        public string? RecommendedSize { get; set; }
        public string? PrimaryImage { get; set; }
    }
}
