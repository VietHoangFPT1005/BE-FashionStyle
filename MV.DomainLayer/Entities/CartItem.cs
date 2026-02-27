using System;
using System.Collections.Generic;

namespace MV.DomainLayer.Entities;

public partial class CartItem
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int ProductVariantId { get; set; }

    public int Quantity { get; set; }

    public DateTime? AddedAt { get; set; }

    public virtual ProductVariant ProductVariant { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
