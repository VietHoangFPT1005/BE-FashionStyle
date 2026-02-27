using System;
using System.Collections.Generic;

namespace MV.DomainLayer.Entities;

public partial class ProductVariant
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public string Sku { get; set; } = null!;

    public string Size { get; set; } = null!;

    public string Color { get; set; } = null!;

    public int? StockQuantity { get; set; }

    public decimal? PriceAdjustment { get; set; }

    public bool? IsActive { get; set; }

    public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual Product Product { get; set; } = null!;
}
