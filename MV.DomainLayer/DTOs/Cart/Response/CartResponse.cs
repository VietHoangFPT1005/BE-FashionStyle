namespace MV.DomainLayer.DTOs.Cart.Response
{
    public class CartResponse
    {
        public List<CartItemResponse> Items { get; set; } = new();
        public CartSummary Summary { get; set; } = new();
    }

    public class CartItemResponse
    {
        public int CartItemId { get; set; }
        public int Quantity { get; set; }
        public CartVariantInfo Variant { get; set; } = new();
        public CartProductInfo Product { get; set; } = new();
        public decimal PriceAdjustment { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal ItemTotal { get; set; }
    }

    public class CartVariantInfo
    {
        public int VariantId { get; set; }
        public string Sku { get; set; } = null!;
        public string Size { get; set; } = null!;
        public string Color { get; set; } = null!;
    }

    public class CartProductInfo
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = null!;
        public decimal Price { get; set; }
        public decimal? SalePrice { get; set; }
        public string? PrimaryImage { get; set; }
        public bool InStock { get; set; }
        public int StockQuantity { get; set; }
    }

    public class CartSummary
    {
        public int TotalItems { get; set; }
        public decimal Subtotal { get; set; }
        public decimal ShippingFee { get; set; } = 30000;
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
    }
}
