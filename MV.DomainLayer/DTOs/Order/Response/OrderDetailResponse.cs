namespace MV.DomainLayer.DTOs.Order.Response
{
    public class OrderDetailResponse
    {
        public int OrderId { get; set; }
        public string OrderCode { get; set; } = null!;
        public string Status { get; set; } = null!;
        public decimal Subtotal { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
        public string? Note { get; set; }
        public OrderShippingInfo ShippingInfo { get; set; } = new();
        public List<OrderItemDetail> Items { get; set; } = new();
        public OrderPaymentInfo? Payment { get; set; }
        public OrderTimeline Timeline { get; set; } = new();

        // Frontend: which buttons/actions to show based on current status
        public List<string> AllowedActions { get; set; } = new();

        // Admin/Staff only fields
        public OrderCustomerInfo? Customer { get; set; }
        public OrderShipperInfo? Shipper { get; set; }
    }

    public class OrderShippingInfo
    {
        public string Name { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public string Address { get; set; } = null!;
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
    }

    public class OrderItemDetail
    {
        public int OrderItemId { get; set; }
        public string ProductName { get; set; } = null!;
        public string? ProductImage { get; set; }
        public string Size { get; set; } = null!;
        public string Color { get; set; } = null!;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public decimal Subtotal { get; set; }
        public int? ProductVariantId { get; set; }
    }

    public class OrderPaymentInfo
    {
        public string PaymentMethod { get; set; } = null!;
        public decimal Amount { get; set; }
        public string? Status { get; set; }
        public string? TransactionId { get; set; }
        public DateTime? PaidAt { get; set; }
    }

    public class OrderTimeline
    {
        public DateTime? CreatedAt { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public DateTime? ShippedAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public DateTime? CancelledAt { get; set; }
    }

    public class OrderCustomerInfo
    {
        public int UserId { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
    }

    public class OrderShipperInfo
    {
        public int UserId { get; set; }
        public string? FullName { get; set; }
        public string? Phone { get; set; }
    }
}
