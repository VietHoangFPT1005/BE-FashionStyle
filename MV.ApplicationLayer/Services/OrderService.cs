using Microsoft.EntityFrameworkCore;
using MV.ApplicationLayer.ServiceInterfaces;
using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.DTOs.Order.Request;
using MV.DomainLayer.DTOs.Order.Response;
using MV.DomainLayer.Entities;
using MV.InfrastructureLayer.DBContext;
using MV.InfrastructureLayer.Interfaces;

namespace MV.ApplicationLayer.Services
{
    public class OrderService : IOrderService
    {
        private readonly FashionDbContext _context;
        private readonly IOrderRepository _orderRepository;
        private readonly ICartItemRepository _cartItemRepository;
        private readonly IUserAddressRepository _addressRepository;
        private readonly IVoucherRepository _voucherRepository;
        private readonly IUserRepository _userRepository;
        private readonly INotificationRepository _notificationRepository;

        public OrderService(
            FashionDbContext context,
            IOrderRepository orderRepository,
            ICartItemRepository cartItemRepository,
            IUserAddressRepository addressRepository,
            IVoucherRepository voucherRepository,
            IUserRepository userRepository,
            INotificationRepository notificationRepository)
        {
            _context = context;
            _orderRepository = orderRepository;
            _cartItemRepository = cartItemRepository;
            _addressRepository = addressRepository;
            _voucherRepository = voucherRepository;
            _userRepository = userRepository;
            _notificationRepository = notificationRepository;
        }

        #region Customer APIs

        public async Task<ApiResponse<CreateOrderResponse>> CreateOrderAsync(int userId, CreateOrderRequest request)
        {
            // Use transaction for atomicity
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Get cart items
                var cartItems = await _cartItemRepository.GetByUserIdAsync(userId);
                if (!cartItems.Any())
                    return ApiResponse<CreateOrderResponse>.ErrorResponse(
                        "Your cart is empty. Please add products before placing an order.");

                // 2. Validate address belongs to user
                var address = await _addressRepository.GetByIdAndUserIdAsync(request.AddressId, userId);
                if (address == null)
                    return ApiResponse<CreateOrderResponse>.ErrorResponse(
                        "Shipping address not found.");

                // 3. Check stock for all items
                var outOfStockItems = new List<object>();
                foreach (var ci in cartItems)
                {
                    var variant = ci.ProductVariant;
                    // Re-check stock from DB (fresh read)
                    var freshVariant = await _context.ProductVariants
                        .FirstOrDefaultAsync(v => v.Id == variant.Id);

                    if (freshVariant == null || (freshVariant.StockQuantity ?? 0) < ci.Quantity)
                    {
                        outOfStockItems.Add(new
                        {
                            productName = $"{variant.Product.Name} {variant.Size} {variant.Color}",
                            available = freshVariant?.StockQuantity ?? 0,
                            requested = ci.Quantity
                        });
                    }
                }

                if (outOfStockItems.Any())
                    return ApiResponse<CreateOrderResponse>.ErrorResponse(
                        "Some products are out of stock.");

                // 4. Calculate prices
                decimal subtotal = 0;
                var orderItemsList = new List<OrderItem>();

                foreach (var ci in cartItems)
                {
                    var variant = ci.ProductVariant;
                    var product = variant.Product;
                    var primaryImage = product.ProductImages
                        .FirstOrDefault(img => img.IsPrimary == true)?.ImageUrl;

                    var basePrice = product.SalePrice ?? product.Price;
                    var unitPrice = basePrice + (variant.PriceAdjustment ?? 0);
                    var itemSubtotal = unitPrice * ci.Quantity;
                    subtotal += itemSubtotal;

                    orderItemsList.Add(new OrderItem
                    {
                        ProductVariantId = variant.Id,
                        ProductName = product.Name,
                        ProductImage = primaryImage,
                        Size = variant.Size,
                        Color = variant.Color,
                        Price = unitPrice,
                        Quantity = ci.Quantity,
                        Subtotal = itemSubtotal
                    });
                }

                // 5. Validate and apply voucher
                decimal discount = 0;
                int? voucherId = null;

                if (!string.IsNullOrWhiteSpace(request.VoucherCode))
                {
                    var voucher = await _voucherRepository.GetByCodeAsync(request.VoucherCode);
                    if (voucher == null || voucher.IsActive != true)
                        return ApiResponse<CreateOrderResponse>.ErrorResponse("Invalid voucher code.");
                    if (voucher.StartDate > DateTime.Now)
                        return ApiResponse<CreateOrderResponse>.ErrorResponse("This voucher is not yet active.");
                    if (voucher.EndDate < DateTime.Now)
                        return ApiResponse<CreateOrderResponse>.ErrorResponse("This voucher has expired.");
                    if (voucher.UsageLimit.HasValue && (voucher.UsedCount ?? 0) >= voucher.UsageLimit.Value)
                        return ApiResponse<CreateOrderResponse>.ErrorResponse("This voucher has reached its usage limit.");
                    if (voucher.MinOrderAmount.HasValue && subtotal < voucher.MinOrderAmount.Value)
                        return ApiResponse<CreateOrderResponse>.ErrorResponse(
                            $"Minimum order amount is {voucher.MinOrderAmount.Value:N0}₫ to use this voucher.");

                    // Calculate discount
                    if (voucher.DiscountType.ToUpper() == "PERCENTAGE")
                    {
                        discount = subtotal * voucher.DiscountValue / 100;
                        if (voucher.MaxDiscountAmount.HasValue && discount > voucher.MaxDiscountAmount.Value)
                            discount = voucher.MaxDiscountAmount.Value;
                    }
                    else
                    {
                        discount = voucher.DiscountValue;
                        if (discount > subtotal) discount = subtotal;
                    }

                    voucherId = voucher.Id;

                    // Increment UsedCount
                    voucher.UsedCount = (voucher.UsedCount ?? 0) + 1;
                    _context.Vouchers.Update(voucher);
                }

                decimal shippingFee = 30000;
                decimal total = subtotal + shippingFee - discount;

                // 6. Generate order code
                var todayCount = await _orderRepository.GetTodayOrderCountAsync();
                var orderCode = $"ORD-{DateTime.Now:yyyyMMdd}-{(todayCount + 1):D4}";

                // 7. Build full shipping address string
                var fullAddress = BuildFullAddress(address);

                // 8. Create Order
                var order = new Order
                {
                    OrderCode = orderCode,
                    UserId = userId,
                    ShippingName = address.ReceiverName,
                    ShippingPhone = address.Phone,
                    ShippingAddress = fullAddress,
                    ShippingCity = address.City,
                    ShippingDistrict = address.District,
                    ShippingWard = address.Ward,
                    ShippingLatitude = address.Latitude,
                    ShippingLongitude = address.Longitude,
                    Subtotal = subtotal,
                    ShippingFee = shippingFee,
                    Discount = discount,
                    Total = total,
                    VoucherId = voucherId,
                    Status = "PENDING",
                    Note = request.Note,
                    DeliveryAttempts = 0,
                    CreatedAt = DateTime.Now
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync(); // Get order.Id

                // 9. Create OrderItems
                foreach (var oi in orderItemsList)
                {
                    oi.OrderId = order.Id;
                }
                _context.OrderItems.AddRange(orderItemsList);

                // 10. Deduct stock + increase sold count
                foreach (var ci in cartItems)
                {
                    await _context.ProductVariants
                        .Where(v => v.Id == ci.ProductVariantId)
                        .ExecuteUpdateAsync(s => s.SetProperty(
                            v => v.StockQuantity, v => (v.StockQuantity ?? 0) - ci.Quantity));

                    await _context.Products
                        .Where(p => p.Id == ci.ProductVariant.ProductId)
                        .ExecuteUpdateAsync(s => s.SetProperty(
                            p => p.SoldCount, p => (p.SoldCount ?? 0) + ci.Quantity));
                }

                // 11. Create Payment record
                var payment = new Payment
                {
                    OrderId = order.Id,
                    PaymentMethod = request.PaymentMethod,
                    Amount = total,
                    Status = "PENDING",
                    CreatedAt = DateTime.Now
                };
                _context.Payments.Add(payment);

                // 12. Clear cart
                await _context.CartItems
                    .Where(ci => ci.UserId == userId)
                    .ExecuteDeleteAsync();

                // 13. Create notification
                var notification = new Notification
                {
                    UserId = userId,
                    Type = "ORDER",
                    Title = "Order placed successfully",
                    Message = $"Your order {orderCode} has been placed successfully.",
                    Data = $"{{\"orderId\":{order.Id}}}",
                    IsRead = false,
                    CreatedAt = DateTime.Now
                };
                _context.Notifications.Add(notification);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Build response
                var response = new CreateOrderResponse
                {
                    OrderId = order.Id,
                    OrderCode = orderCode,
                    Status = "PENDING",
                    Subtotal = subtotal,
                    ShippingFee = shippingFee,
                    Discount = discount,
                    Total = total,
                    PaymentMethod = request.PaymentMethod,
                    Items = orderItemsList.Select(oi => new OrderItemSummary
                    {
                        ProductName = oi.ProductName,
                        Size = oi.Size,
                        Color = oi.Color,
                        Price = oi.Price,
                        Quantity = oi.Quantity,
                        Subtotal = oi.Subtotal
                    }).ToList(),
                    ShippingAddress = new ShippingAddressInfo
                    {
                        ReceiverName = address.ReceiverName,
                        Phone = address.Phone,
                        Address = fullAddress
                    },
                    CreatedAt = order.CreatedAt
                };

                return ApiResponse<CreateOrderResponse>.SuccessResponse(response, "Order placed successfully.");
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<ApiResponse<PaginatedResponse<OrderListResponse>>> GetMyOrdersAsync(
            int userId, int page, int pageSize, string? status)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 50) pageSize = 50;

            var (orders, totalCount) = await _orderRepository.GetByUserIdPagedAsync(
                userId, page, pageSize, status);

            var response = new PaginatedResponse<OrderListResponse>
            {
                Items = orders.Select(o => new OrderListResponse
                {
                    OrderId = o.Id,
                    OrderCode = o.OrderCode,
                    Status = o.Status ?? "PENDING",
                    Total = o.Total,
                    TotalItems = o.OrderItems.Sum(oi => oi.Quantity),
                    PaymentMethod = o.Payment?.PaymentMethod ?? "COD",
                    PaymentStatus = o.Payment?.Status,
                    FirstItemImage = o.OrderItems.FirstOrDefault()?.ProductImage,
                    CreatedAt = o.CreatedAt
                }).ToList(),
                Pagination = new PaginationInfo
                {
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalItems = totalCount,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                    HasNext = page * pageSize < totalCount,
                    HasPrevious = page > 1
                }
            };

            return ApiResponse<PaginatedResponse<OrderListResponse>>.SuccessResponse(response);
        }

        public async Task<ApiResponse<OrderDetailResponse>> GetOrderDetailAsync(int orderId, int userId)
        {
            var order = await _orderRepository.GetByIdAndUserIdWithDetailsAsync(orderId, userId);
            if (order == null)
                return ApiResponse<OrderDetailResponse>.ErrorResponse("Order not found.");

            return ApiResponse<OrderDetailResponse>.SuccessResponse(MapToOrderDetail(order, false));
        }

        public async Task<ApiResponse<object>> CancelOrderAsync(int orderId, int userId, CancelOrderRequest request)
        {
            var order = await _orderRepository.GetByIdAndUserIdWithDetailsAsync(orderId, userId);
            if (order == null)
                return ApiResponse<object>.ErrorResponse("Order not found.");

            if (order.Status != "PENDING")
                return ApiResponse<object>.ErrorResponse(
                    "Only orders with PENDING status can be cancelled.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Update order status
                order.Status = "CANCELLED";
                order.CancelReason = request.CancelReason;
                order.CancelledAt = DateTime.Now;

                // Restore stock + decrease sold count
                foreach (var oi in order.OrderItems)
                {
                    if (oi.ProductVariantId.HasValue)
                    {
                        await _context.ProductVariants
                            .Where(v => v.Id == oi.ProductVariantId.Value)
                            .ExecuteUpdateAsync(s => s.SetProperty(
                                v => v.StockQuantity, v => (v.StockQuantity ?? 0) + oi.Quantity));

                        var variant = await _context.ProductVariants
                            .FirstOrDefaultAsync(v => v.Id == oi.ProductVariantId.Value);
                        if (variant != null)
                        {
                            await _context.Products
                                .Where(p => p.Id == variant.ProductId)
                                .ExecuteUpdateAsync(s => s.SetProperty(
                                    p => p.SoldCount, p => Math.Max(0, (p.SoldCount ?? 0) - oi.Quantity)));
                        }
                    }
                }

                // Handle payment refund
                if (order.Payment != null && order.Payment.PaymentMethod == "SEPAY"
                    && order.Payment.Status == "COMPLETED")
                {
                    order.Payment.Status = "REFUND_PENDING";
                }

                _context.Orders.Update(order);

                // Create notification
                var notification = new Notification
                {
                    UserId = userId,
                    Type = "ORDER",
                    Title = "Order cancelled",
                    Message = $"Your order {order.OrderCode} has been cancelled.",
                    Data = $"{{\"orderId\":{order.Id}}}",
                    IsRead = false,
                    CreatedAt = DateTime.Now
                };
                _context.Notifications.Add(notification);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return ApiResponse<object>.SuccessResponse("Order cancelled successfully.");
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        #endregion

        #region Admin/Staff APIs

        public async Task<ApiResponse<PaginatedResponse<AdminOrderListResponse>>> GetAllOrdersAsync(
            int page, int pageSize, string? status, string? search,
            DateTime? startDate, DateTime? endDate)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            var (orders, totalCount) = await _orderRepository.GetAllOrdersPagedAsync(
                page, pageSize, status, search, startDate, endDate);

            var response = new PaginatedResponse<AdminOrderListResponse>
            {
                Items = orders.Select(o => new AdminOrderListResponse
                {
                    OrderId = o.Id,
                    OrderCode = o.OrderCode,
                    Customer = new OrderCustomerInfo
                    {
                        UserId = o.User.Id,
                        FullName = o.User.FullName,
                        Phone = o.User.Phone
                    },
                    Shipper = o.Shipper != null ? new OrderShipperInfo
                    {
                        UserId = o.Shipper.Id,
                        FullName = o.Shipper.FullName,
                        Phone = o.Shipper.Phone
                    } : null,
                    Status = o.Status ?? "PENDING",
                    Total = o.Total,
                    PaymentMethod = o.Payment?.PaymentMethod ?? "COD",
                    PaymentStatus = o.Payment?.Status,
                    CreatedAt = o.CreatedAt
                }).ToList(),
                Pagination = new PaginationInfo
                {
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalItems = totalCount,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                    HasNext = page * pageSize < totalCount,
                    HasPrevious = page > 1
                }
            };

            return ApiResponse<PaginatedResponse<AdminOrderListResponse>>.SuccessResponse(response);
        }

        public async Task<ApiResponse<OrderDetailResponse>> GetAdminOrderDetailAsync(int orderId)
        {
            var order = await _orderRepository.GetByIdWithDetailsAsync(orderId);
            if (order == null)
                return ApiResponse<OrderDetailResponse>.ErrorResponse("Order not found.");

            return ApiResponse<OrderDetailResponse>.SuccessResponse(MapToOrderDetail(order, true));
        }

        public async Task<ApiResponse<object>> ConfirmOrderAsync(int orderId)
        {
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null)
                return ApiResponse<object>.ErrorResponse("Order not found.");

            if (order.Status != "PENDING")
                return ApiResponse<object>.ErrorResponse(
                    "Only orders with PENDING status can be confirmed.");

            order.Status = "CONFIRMED";
            order.ConfirmedAt = DateTime.Now;
            await _orderRepository.UpdateAsync(order);

            // Notification to customer
            await _notificationRepository.CreateAsync(new Notification
            {
                UserId = order.UserId,
                Type = "ORDER",
                Title = "Order confirmed",
                Message = $"Your order {order.OrderCode} has been confirmed.",
                Data = $"{{\"orderId\":{order.Id}}}",
                IsRead = false,
                CreatedAt = DateTime.Now
            });

            return ApiResponse<object>.SuccessResponse("Order confirmed successfully.");
        }

        public async Task<ApiResponse<object>> AssignShipperAsync(int orderId, AssignShipperRequest request)
        {
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null)
                return ApiResponse<object>.ErrorResponse("Order not found.");

            if (order.Status != "CONFIRMED")
                return ApiResponse<object>.ErrorResponse(
                    "Only orders with CONFIRMED status can be assigned to a shipper.");

            // Validate shipper: must be User with Role = 4 and IsActive
            var shipper = await _userRepository.GetByIdAsync(request.ShipperId);
            if (shipper == null || shipper.Role != 4 || shipper.IsActive != true)
                return ApiResponse<object>.ErrorResponse(
                    "Invalid shipper. The user must be an active shipper (Role = 4).");

            order.ShipperId = request.ShipperId;
            order.Status = "PROCESSING";
            await _orderRepository.UpdateAsync(order);

            // Notification to shipper
            await _notificationRepository.CreateAsync(new Notification
            {
                UserId = request.ShipperId,
                Type = "ORDER",
                Title = "New delivery assigned",
                Message = $"You have been assigned to deliver order {order.OrderCode}.",
                Data = $"{{\"orderId\":{order.Id}}}",
                IsRead = false,
                CreatedAt = DateTime.Now
            });

            // Notification to customer
            await _notificationRepository.CreateAsync(new Notification
            {
                UserId = order.UserId,
                Type = "ORDER",
                Title = "Order is being prepared",
                Message = $"Your order {order.OrderCode} is being prepared for delivery.",
                Data = $"{{\"orderId\":{order.Id}}}",
                IsRead = false,
                CreatedAt = DateTime.Now
            });

            return ApiResponse<object>.SuccessResponse(new
            {
                shipperId = shipper.Id,
                shipperName = shipper.FullName
            }, "Shipper assigned successfully.");
        }

        public async Task<ApiResponse<object>> AdminCancelOrderAsync(int orderId, CancelOrderRequest request)
        {
            var order = await _orderRepository.GetByIdWithDetailsAsync(orderId);
            if (order == null)
                return ApiResponse<object>.ErrorResponse("Order not found.");

            if (order.Status == "DELIVERED")
                return ApiResponse<object>.ErrorResponse(
                    "Cannot cancel a delivered order.");

            if (order.Status == "CANCELLED")
                return ApiResponse<object>.ErrorResponse(
                    "This order is already cancelled.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                order.Status = "CANCELLED";
                order.CancelReason = request.CancelReason;
                order.CancelledAt = DateTime.Now;

                // Restore stock + decrease sold count
                foreach (var oi in order.OrderItems)
                {
                    if (oi.ProductVariantId.HasValue)
                    {
                        await _context.ProductVariants
                            .Where(v => v.Id == oi.ProductVariantId.Value)
                            .ExecuteUpdateAsync(s => s.SetProperty(
                                v => v.StockQuantity, v => (v.StockQuantity ?? 0) + oi.Quantity));

                        var variant = await _context.ProductVariants
                            .FirstOrDefaultAsync(v => v.Id == oi.ProductVariantId.Value);
                        if (variant != null)
                        {
                            await _context.Products
                                .Where(p => p.Id == variant.ProductId)
                                .ExecuteUpdateAsync(s => s.SetProperty(
                                    p => p.SoldCount, p => Math.Max(0, (p.SoldCount ?? 0) - oi.Quantity)));
                        }
                    }
                }

                // Handle refund if SePay completed
                if (order.Payment != null && order.Payment.PaymentMethod == "SEPAY"
                    && order.Payment.Status == "COMPLETED")
                {
                    order.Payment.Status = "REFUND_PENDING";
                }

                _context.Orders.Update(order);

                // Notification to customer
                _context.Notifications.Add(new Notification
                {
                    UserId = order.UserId,
                    Type = "ORDER",
                    Title = "Order cancelled by admin",
                    Message = $"Your order {order.OrderCode} has been cancelled. Reason: {request.CancelReason ?? "N/A"}",
                    Data = $"{{\"orderId\":{order.Id}}}",
                    IsRead = false,
                    CreatedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return ApiResponse<object>.SuccessResponse("Order cancelled successfully.");
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        #endregion

        #region Helpers

        private string BuildFullAddress(UserAddress address)
        {
            var parts = new List<string> { address.AddressLine };
            if (!string.IsNullOrEmpty(address.Ward)) parts.Add(address.Ward);
            parts.Add(address.District);
            parts.Add(address.City);
            return string.Join(", ", parts);
        }

        private OrderDetailResponse MapToOrderDetail(Order order, bool isAdmin)
        {
            var detail = new OrderDetailResponse
            {
                OrderId = order.Id,
                OrderCode = order.OrderCode,
                Status = order.Status ?? "PENDING",
                Subtotal = order.Subtotal,
                ShippingFee = order.ShippingFee ?? 0,
                Discount = order.Discount ?? 0,
                Total = order.Total,
                Note = order.Note,
                ShippingInfo = new OrderShippingInfo
                {
                    Name = order.ShippingName,
                    Phone = order.ShippingPhone,
                    Address = order.ShippingAddress,
                    Latitude = order.ShippingLatitude,
                    Longitude = order.ShippingLongitude
                },
                Items = order.OrderItems.Select(oi => new OrderItemDetail
                {
                    OrderItemId = oi.Id,
                    ProductName = oi.ProductName,
                    ProductImage = oi.ProductImage,
                    Size = oi.Size,
                    Color = oi.Color,
                    Price = oi.Price,
                    Quantity = oi.Quantity,
                    Subtotal = oi.Subtotal,
                    ProductVariantId = oi.ProductVariantId
                }).ToList(),
                Payment = order.Payment != null ? new OrderPaymentInfo
                {
                    PaymentMethod = order.Payment.PaymentMethod,
                    Amount = order.Payment.Amount,
                    Status = order.Payment.Status,
                    TransactionId = order.Payment.TransactionId,
                    PaidAt = order.Payment.PaidAt
                } : null,
                Timeline = new OrderTimeline
                {
                    CreatedAt = order.CreatedAt,
                    ConfirmedAt = order.ConfirmedAt,
                    ShippedAt = order.ShippedAt,
                    DeliveredAt = order.DeliveredAt,
                    CancelledAt = order.CancelledAt
                }
            };

            if (isAdmin)
            {
                detail.Customer = order.User != null ? new OrderCustomerInfo
                {
                    UserId = order.User.Id,
                    FullName = order.User.FullName,
                    Email = order.User.Email,
                    Phone = order.User.Phone
                } : null;

                detail.Shipper = order.Shipper != null ? new OrderShipperInfo
                {
                    UserId = order.Shipper.Id,
                    FullName = order.Shipper.FullName,
                    Phone = order.Shipper.Phone
                } : null;
            }

            return detail;
        }

        #endregion
    }
}
