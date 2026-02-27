using System;
using System.Collections.Generic;

namespace MV.DomainLayer.Entities;

public partial class SizeGuide
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public string SizeName { get; set; } = null!;

    public decimal? MinBust { get; set; }

    public decimal? MaxBust { get; set; }

    public decimal? MinWaist { get; set; }

    public decimal? MaxWaist { get; set; }

    public decimal? MinHips { get; set; }

    public decimal? MaxHips { get; set; }

    public decimal? MinWeight { get; set; }

    public decimal? MaxWeight { get; set; }

    public decimal? ChestCm { get; set; }

    public decimal? WaistCm { get; set; }

    public decimal? HipCm { get; set; }

    public decimal? ShoulderCm { get; set; }

    public decimal? LengthCm { get; set; }

    public decimal? SleeveCm { get; set; }

    public virtual Product Product { get; set; } = null!;
}
