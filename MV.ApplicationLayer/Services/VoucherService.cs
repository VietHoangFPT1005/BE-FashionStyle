using Microsoft.EntityFrameworkCore;
using MV.ApplicationLayer.ServiceInterfaces;
using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.DTOs.Voucher.Request;
using MV.DomainLayer.DTOs.Voucher.Response;
using MV.InfrastructureLayer.DBContext;
using MV.InfrastructureLayer.Interfaces;

namespace MV.ApplicationLayer.Services
{
    public class VoucherService : IVoucherService
    {
        private readonly FashionDbContext _context;
        private readonly IVoucherRepository _voucherRepository;
        private readonly ICartItemRepository _cartItemRepository;
        private readonly IProductVariantRepository _variantRepository;

        public VoucherService(
            FashionDbContext context,
            IVoucherRepository voucherRepository,
            ICartItemRepository cartItemRepository,
            IProductVariantRepository variantRepository)
        {
            _context = context;
            _voucherRepository = voucherRepository;
            _cartItemRepository = cartItemRepository;
            _variantRepository = variantRepository;
        }

        public async Task<ApiResponse<VoucherValidationResponse>> ValidateVoucherAsync(
            int userId, ValidateVoucherRequest request)
        {
            // Find voucher by code
            var voucher = await _voucherRepository.GetByCodeAsync(request.VoucherCode);

            // Check 1 & 2: Voucher exists and is active
            if (voucher == null || voucher.IsActive != true)
                return ApiResponse<VoucherValidationResponse>.ErrorResponse(
                    "Invalid voucher code.");

            // Check 3: StartDate
            if (voucher.StartDate > DateTime.Now)
                return ApiResponse<VoucherValidationResponse>.ErrorResponse(
                    "This voucher is not yet active.");

            // Check 4: EndDate
            if (voucher.EndDate < DateTime.Now)
                return ApiResponse<VoucherValidationResponse>.ErrorResponse(
                    "This voucher has expired.");

            // Check 5: Usage limit
            if (voucher.UsageLimit.HasValue && (voucher.UsedCount ?? 0) >= voucher.UsageLimit.Value)
                return ApiResponse<VoucherValidationResponse>.ErrorResponse(
                    "This voucher has reached its usage limit.");

            // Calculate cart subtotal
            var cartItems = await _cartItemRepository.GetByUserIdAsync(userId);
            if (!cartItems.Any())
                return ApiResponse<VoucherValidationResponse>.ErrorResponse(
                    "Your cart is empty.");

            decimal subtotal = 0;
            foreach (var ci in cartItems)
            {
                var variant = ci.ProductVariant;
                var product = variant.Product;
                var basePrice = product.SalePrice ?? product.Price;
                var unitPrice = basePrice + (variant.PriceAdjustment ?? 0);
                subtotal += unitPrice * ci.Quantity;
            }

            // Check 6: Minimum order amount
            if (voucher.MinOrderAmount.HasValue && subtotal < voucher.MinOrderAmount.Value)
                return ApiResponse<VoucherValidationResponse>.ErrorResponse(
                    $"Minimum order amount is {voucher.MinOrderAmount.Value:N0}₫ to use this voucher. Current subtotal: {subtotal:N0}₫.");

            // Calculate discount
            decimal discount;
            string message;

            if (voucher.DiscountType.ToUpper() == "PERCENTAGE")
            {
                discount = subtotal * voucher.DiscountValue / 100;
                if (voucher.MaxDiscountAmount.HasValue && discount > voucher.MaxDiscountAmount.Value)
                    discount = voucher.MaxDiscountAmount.Value;
                message = $"Applied successfully: {voucher.DiscountValue}% off";
            }
            else // FIXED_AMOUNT
            {
                discount = voucher.DiscountValue;
                if (discount > subtotal) discount = subtotal;
                message = $"Applied successfully: {voucher.DiscountValue:N0}₫ off";
            }

            var shippingFee = 30000m;
            var newTotal = subtotal - discount + shippingFee;

            var response = new VoucherValidationResponse
            {
                VoucherId = voucher.Id,
                Code = voucher.Code,
                Description = voucher.Description,
                DiscountType = voucher.DiscountType,
                DiscountValue = voucher.DiscountValue,
                CalculatedDiscount = discount,
                MaxDiscountAmount = voucher.MaxDiscountAmount,
                CartSubtotal = subtotal,
                NewTotal = newTotal,
                Message = message
            };

            return ApiResponse<VoucherValidationResponse>.SuccessResponse(response);
        }

        // ==================== Get Available Vouchers (Customer) ====================
        public async Task<ApiResponse<List<VoucherResponse>>> GetAvailableVouchersAsync()
        {
            var now = DateTime.Now;
            var vouchers = await _context.Vouchers
                .Where(v => v.IsActive == true
                    && v.StartDate <= now
                    && v.EndDate >= now
                    && (!v.UsageLimit.HasValue || (v.UsedCount ?? 0) < v.UsageLimit.Value))
                .OrderByDescending(v => v.CreatedAt)
                .ToListAsync();

            var response = vouchers.Select(v => new VoucherResponse
            {
                VoucherId = v.Id,
                Code = v.Code,
                Description = v.Description,
                DiscountType = v.DiscountType,
                DiscountValue = v.DiscountValue,
                MinOrderAmount = v.MinOrderAmount,
                MaxDiscountAmount = v.MaxDiscountAmount,
                StartDate = v.StartDate,
                EndDate = v.EndDate
            }).ToList();

            return ApiResponse<List<VoucherResponse>>.SuccessResponse(response);
        }
    }
}
