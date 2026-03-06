using System;
using System.Collections.Generic;

namespace MV.DomainLayer.Entities;

public partial class Order
{
    public int Id { get; set; }

    public string OrderCode { get; set; } = null!;

    public int UserId { get; set; }

    public int? ShipperId { get; set; }

    public string ShippingName { get; set; } = null!;

    public string ShippingPhone { get; set; } = null!;

    public string ShippingAddress { get; set; } = null!;

    public string? ShippingCity { get; set; }

    public string? ShippingDistrict { get; set; }

    public string? ShippingWard { get; set; }

    public decimal? ShippingLatitude { get; set; }

    public decimal? ShippingLongitude { get; set; }

    public decimal Subtotal { get; set; }

    public decimal? ShippingFee { get; set; }

    public decimal? Discount { get; set; }

    public decimal Total { get; set; }

    public int? VoucherId { get; set; }

    public string? Status { get; set; }

    public string? CancelReason { get; set; }

    public string? Note { get; set; }

    public int? DeliveryAttempts { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public DateTime? ShippedAt { get; set; }

    public DateTime? DeliveredAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual Payment? Payment { get; set; }

    public virtual ICollection<ProductReview> ProductReviews { get; set; } = new List<ProductReview>();

    public virtual Refund? Refund { get; set; }

    public virtual User? Shipper { get; set; }

    public virtual ICollection<ShipperLocation> ShipperLocations { get; set; } = new List<ShipperLocation>();

    public virtual User User { get; set; } = null!;

    public virtual Voucher? Voucher { get; set; }
}
