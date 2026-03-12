using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MV.ApplicationLayer.ServiceInterfaces;
using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.DTOs.Refund.Request;
using MV.DomainLayer.DTOs.Refund.Response;
using MV.InfrastructureLayer.DBContext;

namespace MV.ApplicationLayer.Services
{
    public class RefundService : IRefundService
    {
        private readonly FashionDbContext _context;
        private readonly ILogger<RefundService> _logger;

        public RefundService(FashionDbContext context, ILogger<RefundService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ApiResponse<RefundResponse>> RequestRefundAsync(
            int userId, int orderId, CreateRefundRequest request)
        {
            var order = await _context.Orders
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

            if (order == null)
                return ApiResponse<RefundResponse>.ErrorResponse("Order not found.");

            if (order.Status != "DELIVERED")
                return ApiResponse<RefundResponse>.ErrorResponse("Only delivered orders can be refunded.");

            // Check if delivered within 7 days
            if (order.DeliveredAt.HasValue && (DateTime.Now - order.DeliveredAt.Value).TotalDays > 7)
                return ApiResponse<RefundResponse>.ErrorResponse("Refund request must be within 7 days of delivery.");

            // Check if refund already exists
            var existingRefund = await _context.Refunds
                .AnyAsync(r => r.OrderId == orderId);
            if (existingRefund)
                return ApiResponse<RefundResponse>.ErrorResponse("A refund request already exists for this order.");

            var refund = new DomainLayer.Entities.Refund
            {
                OrderId = orderId,
                UserId = userId,
                Reason = request.Reason,
                Status = "PENDING",
                CreatedAt = DateTime.Now
            };

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Refunds.Add(refund);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return ApiResponse<RefundResponse>.SuccessResponse(new RefundResponse
                {
                    RefundId = refund.Id,
                    OrderId = orderId,
                    OrderCode = order.OrderCode,
                    UserId = userId,
                    CustomerName = order.User.FullName,
                    Reason = refund.Reason,
                    Status = refund.Status!,
                    OrderTotal = order.Total,
                    CreatedAt = refund.CreatedAt
                }, "Refund request submitted successfully.");
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<ApiResponse<RefundResponse>> GetRefundByOrderAsync(int userId, int orderId)
        {
            var refund = await _context.Refunds
                .Include(r => r.Order)
                .ThenInclude(o => o.User)
                .FirstOrDefaultAsync(r => r.OrderId == orderId && r.UserId == userId);

            if (refund == null)
                return ApiResponse<RefundResponse>.ErrorResponse("No refund request found for this order.");

            return ApiResponse<RefundResponse>.SuccessResponse(await MapToRefundResponse(refund));
        }

        public async Task<ApiResponse<PaginatedResponse<RefundResponse>>> GetAllRefundsAsync(
            int page, int pageSize, string? status)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 50) pageSize = 50;

            var query = _context.Refunds
                .Include(r => r.Order)
                .ThenInclude(o => o.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(r => r.Status == status.ToUpper());

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var responseItems = new List<RefundResponse>();
            foreach (var item in items)
            {
                responseItems.Add(await MapToRefundResponse(item));
            }

            var response = new PaginatedResponse<RefundResponse>
            {
                Items = responseItems,
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

            return ApiResponse<PaginatedResponse<RefundResponse>>.SuccessResponse(response);
        }

        public async Task<ApiResponse<RefundResponse>> ApproveRefundAsync(
            int adminId, int refundId, ProcessRefundRequest request)
        {
            var refund = await _context.Refunds
                .Include(r => r.Order)
                .ThenInclude(o => o.User)
                .FirstOrDefaultAsync(r => r.Id == refundId);

            if (refund == null)
                return ApiResponse<RefundResponse>.ErrorResponse("Refund not found.");

            if (refund.Status != "PENDING")
                return ApiResponse<RefundResponse>.ErrorResponse("This refund has already been processed.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                refund.Status = "APPROVED";
                refund.AdminNote = request.AdminNote;
                refund.ProcessedAt = DateTime.Now;
                refund.ProcessedBy = adminId;

                // Update order status to REFUNDED
                refund.Order.Status = "REFUNDED";

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("AUDIT: Admin {AdminId} approved Refund {RefundId} for Order {OrderId} at {Time}", adminId, refundId, refund.OrderId, DateTime.UtcNow);

                return ApiResponse<RefundResponse>.SuccessResponse(
                    await MapToRefundResponse(refund), "Refund approved successfully.");
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<ApiResponse<RefundResponse>> RejectRefundAsync(
            int adminId, int refundId, ProcessRefundRequest request)
        {
            var refund = await _context.Refunds
                .Include(r => r.Order)
                .ThenInclude(o => o.User)
                .FirstOrDefaultAsync(r => r.Id == refundId);

            if (refund == null)
                return ApiResponse<RefundResponse>.ErrorResponse("Refund not found.");

            if (refund.Status != "PENDING")
                return ApiResponse<RefundResponse>.ErrorResponse("This refund has already been processed.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                refund.Status = "REJECTED";
                refund.AdminNote = request.AdminNote;
                refund.ProcessedAt = DateTime.Now;
                refund.ProcessedBy = adminId;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("AUDIT: Admin {AdminId} rejected Refund {RefundId} for Order {OrderId}. Note: {Note} at {Time}", adminId, refundId, refund.OrderId, request.AdminNote, DateTime.UtcNow);

                return ApiResponse<RefundResponse>.SuccessResponse(
                    await MapToRefundResponse(refund), "Refund rejected.");
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task<RefundResponse> MapToRefundResponse(DomainLayer.Entities.Refund refund)
        {
            string? processedByName = null;
            if (refund.ProcessedBy.HasValue)
            {
                var admin = await _context.Users.FindAsync(refund.ProcessedBy.Value);
                processedByName = admin?.FullName;
            }

            return new RefundResponse
            {
                RefundId = refund.Id,
                OrderId = refund.OrderId,
                OrderCode = refund.Order.OrderCode,
                UserId = refund.UserId,
                CustomerName = refund.Order.User.FullName,
                Reason = refund.Reason,
                AdminNote = refund.AdminNote,
                Status = refund.Status ?? "PENDING",
                OrderTotal = refund.Order.Total,
                CreatedAt = refund.CreatedAt,
                ProcessedAt = refund.ProcessedAt,
                ProcessedBy = refund.ProcessedBy,
                ProcessedByName = processedByName
            };
        }
    }
}
