using System;
using System.Collections.Generic;

namespace MV.DomainLayer.Entities;

public partial class UserBodyProfile
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public decimal? Height { get; set; }

    public decimal? Weight { get; set; }

    public decimal? Bust { get; set; }

    public decimal? Waist { get; set; }

    public decimal? Hips { get; set; }

    public decimal? Arm { get; set; }

    public decimal? Thigh { get; set; }

    public string? BodyShape { get; set; }

    public string? FitPreference { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
