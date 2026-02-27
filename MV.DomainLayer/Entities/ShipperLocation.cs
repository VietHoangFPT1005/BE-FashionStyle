using System;
using System.Collections.Generic;

namespace MV.DomainLayer.Entities;

public partial class ShipperLocation
{
    public int Id { get; set; }

    public int ShipperId { get; set; }

    public int? OrderId { get; set; }

    public decimal Latitude { get; set; }

    public decimal Longitude { get; set; }

    public decimal? Accuracy { get; set; }

    public decimal? Speed { get; set; }

    public decimal? Heading { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Order? Order { get; set; }

    public virtual User Shipper { get; set; } = null!;
}
