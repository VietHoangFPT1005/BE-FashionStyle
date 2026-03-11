using System;
using System.Collections.Generic;

namespace MV.DomainLayer.Entities;

public partial class Payment
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public string PaymentMethod { get; set; } = null!;

    public decimal Amount { get; set; }

    public string? Status { get; set; }

    public string? TransactionId { get; set; }

    public string? PaymentData { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? ExpiredAt { get; set; }

    public decimal? ReceivedAmount { get; set; }

    public string? BankCode { get; set; }

    public string? PaymentReference { get; set; }

    public string? QrCodeUrl { get; set; }

    public int? VerifiedBy { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual User? VerifiedByNavigation { get; set; }
}
