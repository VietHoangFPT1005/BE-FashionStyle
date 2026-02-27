using System;
using System.Collections.Generic;

namespace MV.DomainLayer.Entities;

public partial class ProductImage
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public string ImageUrl { get; set; } = null!;

    public string? AltText { get; set; }

    public bool? IsPrimary { get; set; }

    public int? SortOrder { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Product Product { get; set; } = null!;
}
