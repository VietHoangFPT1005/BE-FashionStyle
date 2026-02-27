using System;
using System.Collections.Generic;

namespace MV.DomainLayer.Entities;

public partial class ProductReview
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public int UserId { get; set; }

    public int? OrderId { get; set; }

    public int Rating { get; set; }

    public string? Comment { get; set; }

    public string? ReviewImageUrl { get; set; }

    public decimal? HeightCm { get; set; }

    public decimal? WeightKg { get; set; }

    public string? SizeOrdered { get; set; }

    public bool? ShowBodyInfo { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Order? Order { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
