using System;
using System.Collections.Generic;

namespace MV.DomainLayer.Entities;

public partial class SepayTransaction
{
    public int Id { get; set; }

    public int? OrderId { get; set; }

    public string? SepayId { get; set; }

    public string? Gateway { get; set; }

    public DateTime? TransactionDate { get; set; }

    public string? AccountNumber { get; set; }

    public string? TransferType { get; set; }

    public decimal? TransferAmount { get; set; }

    public decimal? Accumulated { get; set; }

    public string? Code { get; set; }

    public string? Content { get; set; }

    public string? ReferenceNumber { get; set; }

    public string? Description { get; set; }

    public bool? IsProcessed { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public string? RawData { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Order? Order { get; set; }
}
