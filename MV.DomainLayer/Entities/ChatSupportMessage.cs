using System;
using System.Collections.Generic;

namespace MV.DomainLayer.Entities;

public partial class ChatSupportMessage
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public int SenderId { get; set; }

    public int SenderRole { get; set; }

    public string Message { get; set; } = null!;

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User Customer { get; set; } = null!;

    public virtual User Sender { get; set; } = null!;
}
