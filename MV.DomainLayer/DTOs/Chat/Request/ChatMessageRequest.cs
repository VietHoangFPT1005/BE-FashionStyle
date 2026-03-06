using System.ComponentModel.DataAnnotations;

namespace MV.DomainLayer.DTOs.Chat.Request
{
    public class ChatMessageRequest
    {
        [Required(ErrorMessage = "Message is required.")]
        [StringLength(1000, ErrorMessage = "Message must not exceed 1000 characters.")]
        public string Message { get; set; } = string.Empty;

        public string? SessionId { get; set; }
    }
}
