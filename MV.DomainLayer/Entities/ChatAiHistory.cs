using System;
using System.Collections.Generic;

namespace MV.DomainLayer.Entities;

public partial class ChatAiHistory
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Role { get; set; } = null!;

    public string Content { get; set; } = null!;

    public List<int>? SuggestedProductIds { get; set; }

    public string? SessionId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
