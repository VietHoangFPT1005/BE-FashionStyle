using System;
using System.Collections.Generic;

namespace MV.DomainLayer.Entities;

public partial class OtpCode
{
    public int Id { get; set; }

    public int? UserId { get; set; }

    public string? Email { get; set; }

    public string Code { get; set; } = null!;

    public string Type { get; set; } = null!;

    public DateTime ExpiredAt { get; set; }

    public bool? IsUsed { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User? User { get; set; }
}
