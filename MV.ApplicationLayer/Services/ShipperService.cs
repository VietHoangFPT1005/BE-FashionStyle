using Microsoft.EntityFrameworkCore;
using MV.ApplicationLayer.ServiceInterfaces;
using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.DTOs.Shipper.Request;
using MV.DomainLayer.DTOs.Shipper.Response;
using MV.DomainLayer.Entities;
using MV.InfrastructureLayer.DBContext;
using MV.InfrastructureLayer.Interfaces;

namespace MV.ApplicationLayer.Services
{
    public class ShipperService : IShipperService
    {
        private readonly FashionDbContext _context;
        private readonly IOrderRepository _orderRepository;
        private readonly IShipperLocationRepository _locationRepository;
        private readonly INotificationRepository _notificationRepository;

        public ShipperService(
            FashionDbContext context,
            IOrderRepository orderRepository,
            IShipperLocationRepository locationRepository,
            INotificationRepository notificationRepository)
        {
            _context = context;
            _orderRepository = orderRepository;
            _locationRepository = locationRepository;
            _notificationRepository = notificationRepository;
        }

        // ==================== API 1: Get Shipper's Orders ====================
        public async Task<ApiResponse<List<ShipperOrderListResponse>>> GetShipperOrdersAsync(
            int shipperId, string? status)
        {
            var query = _context.Orders
                .Include(o => o.OrderItems)
                .Include(o => o.Payment)
                .Where(o => o.ShipperId == shipperId);

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(o => o.Status == status);

            var orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            var response = orders.Select(o => new ShipperOrderListResponse
            {
                OrderId = o.Id,
                OrderCode = o.OrderCode,
                Status = o.Status ?? "PROCESSING",
                Total = o.Total,
                PaymentMethod = o.Payment?.PaymentMethod ?? "COD",
                TotalItems = o.OrderItems.Sum(oi => oi.Quantity),
                ShippingInfo = new ShipperShippingInfo
                {
                    Name = o.ShippingName,
                    Phone = o.ShippingPhone,
                    Address = o.ShippingAddress,
                    Latitude = o.ShippingLatitude,
                    Longitude = o.ShippingLongitude
                },
                CreatedAt = o.CreatedAt
            }).ToList();

            return ApiResponse<List<ShipperOrderListResponse>>.SuccessResponse(response);
        }

        // ==================== API 2: Pickup Order (Start Shipping) ====================
        public async Task<ApiResponse<object>> PickupOrderAsync(int shipperId, int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.ShipperId == shipperId);

            if (order == null)
                return ApiResponse<object>.ErrorResponse("Order not found or not assigned to you.");

            if (order.Status != "PROCESSING")
                return ApiResponse<object>.ErrorResponse(
                    "Order is not in PROCESSING status. Cannot start shipping.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                order.Status = "SHIPPING";
                order.ShippedAt = DateTime.Now;
                _context.Orders.Update(order);

                // Notification to customer
                _context.Notifications.Add(new Notification
                {
                    UserId = order.UserId,
                    Type = "SHIPPING",
                    Title = "Order is being delivered",
                    Message = $"Your order {order.OrderCode} is being delivered to you.",
                    Data = $"{{\"orderId\":{order.Id},\"screen\":\"ORDER_TRACKING\"}}",
                    IsRead = false,
                    CreatedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return ApiResponse<object>.SuccessResponse("Order picked up. Delivery started.");
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // ==================== API 3: Deliver Order (Confirm Delivery) ====================
        public async Task<ApiResponse<object>> DeliverOrderAsync(int shipperId, int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.ShipperId == shipperId);

            if (order == null)
                return ApiResponse<object>.ErrorResponse("Order not found or not assigned to you.");

            if (order.Status != "SHIPPING")
                return ApiResponse<object>.ErrorResponse(
                    "Order is not in SHIPPING status. Cannot confirm delivery.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                order.Status = "DELIVERED";
                order.DeliveredAt = DateTime.Now;

                // If COD payment, mark as completed
                if (order.Payment != null && order.Payment.PaymentMethod == "COD")
                {
                    order.Payment.Status = "COMPLETED";
                    order.Payment.PaidAt = DateTime.Now;
                }

                _context.Orders.Update(order);

                // Notification to customer
                _context.Notifications.Add(new Notification
                {
                    UserId = order.UserId,
                    Type = "ORDER",
                    Title = "Order delivered successfully",
                    Message = $"Your order {order.OrderCode} has been delivered successfully.",
                    Data = $"{{\"orderId\":{order.Id},\"screen\":\"ORDER_DETAIL\"}}",
                    IsRead = false,
                    CreatedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return ApiResponse<object>.SuccessResponse("Order delivered successfully.");
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // ==================== API 4: Delivery Failed ====================
        public async Task<ApiResponse<object>> DeliveryFailedAsync(
            int shipperId, int orderId, DeliveryFailedRequest request)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.ShipperId == shipperId);

            if (order == null)
                return ApiResponse<object>.ErrorResponse("Order not found or not assigned to you.");

            if (order.Status != "SHIPPING")
                return ApiResponse<object>.ErrorResponse(
                    "Order is not in SHIPPING status. Cannot report delivery failure.");

            order.DeliveryAttempts = (order.DeliveryAttempts ?? 0) + 1;
            int maxAttempts = 3;

            if (order.DeliveryAttempts >= maxAttempts)
            {
                // Auto cancel after max attempts
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    order.Status = "CANCELLED";
                    order.CancelReason = $"Delivery failed {maxAttempts} times. Reason: {request.Reason ?? "N/A"}";
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
                        Title = "Order cancelled - delivery failed",
                        Message = $"Your order {order.OrderCode} has been cancelled due to {maxAttempts} failed delivery attempts.",
                        Data = $"{{\"orderId\":{order.Id},\"screen\":\"ORDER_DETAIL\"}}",
                        IsRead = false,
                        CreatedAt = DateTime.Now
                    });

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return ApiResponse<object>.SuccessResponse(new
                    {
                        deliveryAttempts = order.DeliveryAttempts,
                        status = "CANCELLED"
                    }, $"Delivery failed attempt {maxAttempts}. Order has been automatically cancelled.");
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            else
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    _context.Orders.Update(order);

                    // Notification to customer
                    _context.Notifications.Add(new Notification
                    {
                        UserId = order.UserId,
                        Type = "SHIPPING",
                        Title = "Delivery unsuccessful",
                        Message = $"Delivery attempt for order {order.OrderCode} was unsuccessful. Will retry.",
                        Data = $"{{\"orderId\":{order.Id},\"screen\":\"ORDER_TRACKING\"}}",
                        IsRead = false,
                        CreatedAt = DateTime.Now
                    });

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return ApiResponse<object>.SuccessResponse(new
                    {
                        deliveryAttempts = order.DeliveryAttempts,
                        maxAttempts = maxAttempts
                    }, $"Delivery failed. Attempt {order.DeliveryAttempts}/{maxAttempts}.");
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }

        // ==================== API 5: Update Shipper GPS Location ====================
        public async Task<ApiResponse<object>> UpdateLocationAsync(
            int shipperId, UpdateLocationRequest request)
        {
            // Verify order assigned to this shipper and status is SHIPPING
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == request.OrderId
                    && o.ShipperId == shipperId
                    && o.Status == "SHIPPING");

            if (order == null)
                return ApiResponse<object>.ErrorResponse(
                    "Order not found, not assigned to you, or not in SHIPPING status.");

            var location = new ShipperLocation
            {
                ShipperId = shipperId,
                OrderId = request.OrderId,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                Accuracy = request.Accuracy,
                Speed = request.Speed,
                Heading = request.Heading,
                CreatedAt = DateTime.Now
            };

            await _locationRepository.CreateAsync(location);

            return ApiResponse<object>.SuccessResponse("Location updated successfully.");
        }

        // ==================== API 6: Track Order on Map (Customer) ====================
        public async Task<ApiResponse<TrackingResponse>> TrackOrderAsync(int userId, int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.Shipper)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

            if (order == null)
                return ApiResponse<TrackingResponse>.ErrorResponse("Order not found.");

            if (order.Status != "SHIPPING")
                return ApiResponse<TrackingResponse>.ErrorResponse(
                    "Order is not in SHIPPING status. Tracking is only available during delivery.");

            if (!order.ShipperId.HasValue)
                return ApiResponse<TrackingResponse>.ErrorResponse("No shipper assigned to this order.");

            // Get latest shipper location
            var latestLocation = await _locationRepository.GetLatestByOrderIdAsync(orderId, order.ShipperId.Value);

            var response = new TrackingResponse
            {
                OrderId = order.Id,
                OrderCode = order.OrderCode,
                Status = order.Status ?? "SHIPPING",
                Shipper = new TrackingShipperInfo
                {
                    ShipperId = order.ShipperId.Value,
                    FullName = order.Shipper?.FullName,
                    Phone = order.Shipper?.Phone
                },
                CurrentLocation = latestLocation != null ? new TrackingCurrentLocation
                {
                    Latitude = latestLocation.Latitude,
                    Longitude = latestLocation.Longitude,
                    Accuracy = latestLocation.Accuracy,
                    Speed = latestLocation.Speed,
                    Heading = latestLocation.Heading,
                    UpdatedAt = latestLocation.CreatedAt
                } : null,
                Destination = new TrackingDestination
                {
                    Latitude = order.ShippingLatitude,
                    Longitude = order.ShippingLongitude,
                    Address = order.ShippingAddress
                },
                Timeline = new TrackingTimeline
                {
                    ShippedAt = order.ShippedAt,
                    EstimatedMinutes = null
                }
            };

            return ApiResponse<TrackingResponse>.SuccessResponse(response);
        }
    }
}
