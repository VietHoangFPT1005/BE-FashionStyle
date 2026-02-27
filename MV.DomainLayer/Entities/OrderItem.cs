using System;
using System.Collections.Generic;

namespace MV.DomainLayer.Entities;

public partial class OrderItem
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public int? ProductVariantId { get; set; }

    public string ProductName { get; set; } = null!;

    public string? ProductImage { get; set; }

    public string Size { get; set; } = null!;

    public string Color { get; set; } = null!;

    public decimal Price { get; set; }

    public int Quantity { get; set; }

    public decimal Subtotal { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual ProductVariant? ProductVariant { get; set; }
}
