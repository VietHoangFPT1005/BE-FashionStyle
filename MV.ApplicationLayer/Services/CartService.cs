using MV.ApplicationLayer.ServiceInterfaces;
using MV.DomainLayer.DTOs.Cart.Request;
using MV.DomainLayer.DTOs.Cart.Response;
using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.Entities;
using MV.InfrastructureLayer.Interfaces;

namespace MV.ApplicationLayer.Services
{
    public class CartService : ICartService
    {
        private readonly ICartItemRepository _cartItemRepository;
        private readonly IProductVariantRepository _variantRepository;

        public CartService(
            ICartItemRepository cartItemRepository,
            IProductVariantRepository variantRepository)
        {
            _cartItemRepository = cartItemRepository;
            _variantRepository = variantRepository;
        }

        public async Task<ApiResponse<CartResponse>> GetCartAsync(int userId)
        {
            var cartItems = await _cartItemRepository.GetByUserIdAsync(userId);

            var items = new List<CartItemResponse>();
            decimal subtotal = 0;

            foreach (var ci in cartItems)
            {
                var variant = ci.ProductVariant;
                var product = variant.Product;

                var priceAdjustment = variant.PriceAdjustment ?? 0;
                var basePrice = product.SalePrice ?? product.Price;
                var unitPrice = basePrice + priceAdjustment;
                var itemTotal = unitPrice * ci.Quantity;
                subtotal += itemTotal;

                var primaryImage = product.ProductImages
                    .FirstOrDefault(img => img.IsPrimary == true)?.ImageUrl;

                items.Add(new CartItemResponse
                {
                    CartItemId = ci.Id,
                    Quantity = ci.Quantity,
                    Variant = new CartVariantInfo
                    {
                        VariantId = variant.Id,
                        Sku = variant.Sku,
                        Size = variant.Size,
                        Color = variant.Color
                    },
                    Product = new CartProductInfo
                    {
                        ProductId = product.Id,
                        Name = product.Name,
                        Price = product.Price,
                        SalePrice = product.SalePrice,
                        PrimaryImage = primaryImage,
                        InStock = (variant.StockQuantity ?? 0) >= ci.Quantity,
                        StockQuantity = variant.StockQuantity ?? 0
                    },
                    PriceAdjustment = priceAdjustment,
                    UnitPrice = unitPrice,
                    ItemTotal = itemTotal
                });
            }

            var shippingFee = items.Any() ? 30000m : 0m;

            var response = new CartResponse
            {
                Items = items,
                Summary = new CartSummary
                {
                    TotalItems = items.Sum(i => i.Quantity),
                    Subtotal = subtotal,
                    ShippingFee = shippingFee,
                    Discount = 0,
                    Total = subtotal + shippingFee
                }
            };

            return ApiResponse<CartResponse>.SuccessResponse(response);
        }

        public async Task<ApiResponse<object>> AddToCartAsync(int userId, AddToCartRequest request)
        {
            // Validate variant exists and is active
            var variant = await _variantRepository.GetByIdAsync(request.ProductVariantId);
            if (variant == null || variant.IsActive != true)
                return ApiResponse<object>.ErrorResponse("Product variant not found or not available.");

            // Check stock
            var stockQty = variant.StockQuantity ?? 0;
            if (stockQty <= 0)
                return ApiResponse<object>.ErrorResponse("This product is out of stock.");

            // Check if variant already in cart
            var existingItem = await _cartItemRepository.GetByUserIdAndVariantIdAsync(userId, request.ProductVariantId);

            if (existingItem != null)
            {
                // Update quantity
                var newQuantity = existingItem.Quantity + request.Quantity;
                if (newQuantity > stockQty)
                    return ApiResponse<object>.ErrorResponse(
                        $"Requested quantity exceeds available stock. Available: {stockQty}.");

                existingItem.Quantity = newQuantity;
                await _cartItemRepository.UpdateAsync(existingItem);

                var basePrice = variant.Product.SalePrice ?? variant.Product.Price;
                var unitPrice = basePrice + (variant.PriceAdjustment ?? 0);

                return ApiResponse<object>.SuccessResponse(new
                {
                    cartItemId = existingItem.Id,
                    productVariantId = request.ProductVariantId,
                    quantity = newQuantity,
                    unitPrice,
                    itemTotal = unitPrice * newQuantity
                }, "Product added to cart successfully.");
            }
            else
            {
                // Check quantity against stock
                if (request.Quantity > stockQty)
                    return ApiResponse<object>.ErrorResponse(
                        $"Requested quantity exceeds available stock. Available: {stockQty}.");

                var cartItem = new CartItem
                {
                    UserId = userId,
                    ProductVariantId = request.ProductVariantId,
                    Quantity = request.Quantity,
                    AddedAt = DateTime.Now
                };

                await _cartItemRepository.CreateAsync(cartItem);

                var basePrice = variant.Product.SalePrice ?? variant.Product.Price;
                var unitPrice = basePrice + (variant.PriceAdjustment ?? 0);

                return ApiResponse<object>.SuccessResponse(new
                {
                    cartItemId = cartItem.Id,
                    productVariantId = request.ProductVariantId,
                    quantity = request.Quantity,
                    unitPrice,
                    itemTotal = unitPrice * request.Quantity
                }, "Product added to cart successfully.");
            }
        }

        public async Task<ApiResponse<object>> UpdateCartItemAsync(int userId, int cartItemId, UpdateCartItemRequest request)
        {
            var cartItem = await _cartItemRepository.GetByIdAndUserIdAsync(cartItemId, userId);
            if (cartItem == null)
                return ApiResponse<object>.ErrorResponse("Cart item not found.");

            // If quantity = 0, delete the item
            if (request.Quantity == 0)
            {
                await _cartItemRepository.DeleteAsync(cartItem);
                return ApiResponse<object>.SuccessResponse("Cart item removed successfully.");
            }

            // Check stock
            var stockQty = cartItem.ProductVariant.StockQuantity ?? 0;
            if (request.Quantity > stockQty)
                return ApiResponse<object>.ErrorResponse(
                    $"Requested quantity exceeds available stock. Available: {stockQty}.");

            cartItem.Quantity = request.Quantity;
            await _cartItemRepository.UpdateAsync(cartItem);

            return ApiResponse<object>.SuccessResponse(new
            {
                cartItemId = cartItem.Id,
                quantity = request.Quantity
            }, "Cart item quantity updated successfully.");
        }

        public async Task<ApiResponse<object>> RemoveCartItemAsync(int userId, int cartItemId)
        {
            var cartItem = await _cartItemRepository.GetByIdAndUserIdAsync(cartItemId, userId);
            if (cartItem == null)
                return ApiResponse<object>.ErrorResponse("Cart item not found.");

            await _cartItemRepository.DeleteAsync(cartItem);
            return ApiResponse<object>.SuccessResponse("Cart item removed successfully.");
        }

        public async Task<ApiResponse<object>> ClearCartAsync(int userId)
        {
            await _cartItemRepository.DeleteAllByUserIdAsync(userId);
            return ApiResponse<object>.SuccessResponse("Cart cleared successfully.");
        }
    }
}
