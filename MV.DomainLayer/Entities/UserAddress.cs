using System;
using System.Collections.Generic;

namespace MV.DomainLayer.Entities;

public partial class UserAddress
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string ReceiverName { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string AddressLine { get; set; } = null!;

    public string? Ward { get; set; }

    public string District { get; set; } = null!;

    public string City { get; set; } = null!;

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public bool? IsDefault { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
