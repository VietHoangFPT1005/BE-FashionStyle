namespace MV.DomainLayer.DTOs.Order.Response
{
    public class CreateOrderResponse
    {
        public int OrderId { get; set; }
        public string OrderCode { get; set; } = null!;
        public string Status { get; set; } = null!;
        public decimal Subtotal { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
        public string PaymentMethod { get; set; } = null!;
        public List<OrderItemSummary> Items { get; set; } = new();
        public ShippingAddressInfo ShippingAddress { get; set; } = new();
        public DateTime? CreatedAt { get; set; }
    }

    public class OrderItemSummary
    {
        public string ProductName { get; set; } = null!;
        public string Size { get; set; } = null!;
        public string Color { get; set; } = null!;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public decimal Subtotal { get; set; }
    }

    public class ShippingAddressInfo
    {
        public string ReceiverName { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public string Address { get; set; } = null!;
    }
}
