using System;
using System.Collections.Generic;

namespace MV.DomainLayer.Entities;

public partial class RefreshToken
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Token { get; set; } = null!;

    public DateTime ExpiredAt { get; set; }

    public bool? IsRevoked { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
