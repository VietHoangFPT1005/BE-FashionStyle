using System;
using System.Collections.Generic;

namespace MV.DomainLayer.Entities;

public partial class Refund
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public int UserId { get; set; }

    public string Reason { get; set; } = null!;

    public string? AdminNote { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public int? ProcessedBy { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual User? ProcessedByNavigation { get; set; }

    public virtual User User { get; set; } = null!;
}
