using Microsoft.EntityFrameworkCore;
using MV.ApplicationLayer.ServiceInterfaces;
using MV.DomainLayer.DTOs.Admin.Request;
using MV.DomainLayer.DTOs.Admin.Response;
using MV.DomainLayer.DTOs.Common;
using MV.InfrastructureLayer.DBContext;
using MV.InfrastructureLayer.Interfaces;

namespace MV.ApplicationLayer.Services
{
    public class AdminService : IAdminService
    {
        private readonly FashionDbContext _context;
        private readonly IUserRepository _userRepository;
        private readonly IRefreshTokenRepository _refreshTokenRepository;

        public AdminService(
            FashionDbContext context,
            IUserRepository userRepository,
            IRefreshTokenRepository refreshTokenRepository)
        {
            _context = context;
            _userRepository = userRepository;
            _refreshTokenRepository = refreshTokenRepository;
        }

        // ==================== API 16: Change User Role ====================
        public async Task<ApiResponse<object>> ChangeUserRoleAsync(
            int adminId, int userId, ChangeRoleRequest request)
        {
            if (adminId == userId)
                return ApiResponse<object>.ErrorResponse("You cannot change your own role.");

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                return ApiResponse<object>.ErrorResponse("User not found.");

            user.Role = request.Role;
            user.UpdatedAt = DateTime.Now;
            await _userRepository.UpdateAsync(user);

            var roleNames = new Dictionary<int, string>
            {
                { 1, "Admin" }, { 2, "Staff" }, { 3, "Customer" }, { 4, "Shipper" }
            };

            return ApiResponse<object>.SuccessResponse(new
            {
                userId = user.Id,
                newRole = request.Role,
                roleName = roleNames.GetValueOrDefault(request.Role, "Unknown")
            }, "User role updated successfully.");
        }

        // ==================== API 17: Activate/Deactivate User ====================
        public async Task<ApiResponse<object>> ChangeUserStatusAsync(
            int adminId, int userId, ChangeStatusRequest request)
        {
            if (adminId == userId)
                return ApiResponse<object>.ErrorResponse("You cannot change your own account status.");

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                return ApiResponse<object>.ErrorResponse("User not found.");

            user.IsActive = request.IsActive;
            user.UpdatedAt = DateTime.Now;
            await _userRepository.UpdateAsync(user);

            // If deactivating, revoke all refresh tokens (force logout)
            if (!request.IsActive)
            {
                await _refreshTokenRepository.RevokeAllByUserIdAsync(userId);
            }

            var message = request.IsActive
                ? "User account activated successfully."
                : "User account deactivated successfully.";

            return ApiResponse<object>.SuccessResponse(message);
        }

        // ==================== API 18: Admin Dashboard Statistics ====================
        public async Task<ApiResponse<DashboardResponse>> GetDashboardAsync()
        {
            // Overview
            var totalOrders = await _context.Orders.CountAsync();
            var totalRevenue = await _context.Payments
                .Where(p => p.Status == "COMPLETED")
                .SumAsync(p => p.Amount);
            var totalCustomers = await _context.Users.CountAsync(u => u.Role == 3);
            var totalProducts = await _context.Products.CountAsync(p => p.IsActive == true && p.IsDeleted != true);

            // Orders by status
            var ordersByStatus = await _context.Orders
                .GroupBy(o => o.Status ?? "PENDING")
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var statusDict = new Dictionary<string, int>
            {
                { "PENDING", 0 }, { "CONFIRMED", 0 }, { "PROCESSING", 0 },
                { "SHIPPING", 0 }, { "DELIVERED", 0 }, { "CANCELLED", 0 }
            };
            foreach (var item in ordersByStatus)
            {
                statusDict[item.Status] = item.Count;
            }

            // Recent orders (top 5)
            var recentOrders = await _context.Orders
                .Include(o => o.User)
                .OrderByDescending(o => o.CreatedAt)
                .Take(5)
                .Select(o => new DashboardRecentOrder
                {
                    OrderId = o.Id,
                    OrderCode = o.OrderCode,
                    CustomerName = o.User.FullName,
                    Total = o.Total,
                    Status = o.Status ?? "PENDING",
                    CreatedAt = o.CreatedAt
                })
                .ToListAsync();

            // Top products (top 5 by sold count)
            var topProducts = await _context.Products
                .Where(p => p.IsActive == true && p.IsDeleted != true && (p.SoldCount ?? 0) > 0)
                .OrderByDescending(p => p.SoldCount)
                .Take(5)
                .Select(p => new DashboardTopProduct
                {
                    ProductId = p.Id,
                    Name = p.Name,
                    SoldCount = p.SoldCount ?? 0,
                    Revenue = (p.SalePrice ?? p.Price) * (p.SoldCount ?? 0)
                })
                .ToListAsync();

            var response = new DashboardResponse
            {
                Overview = new DashboardOverview
                {
                    TotalOrders = totalOrders,
                    TotalRevenue = totalRevenue,
                    TotalCustomers = totalCustomers,
                    TotalProducts = totalProducts
                },
                OrdersByStatus = statusDict,
                RecentOrders = recentOrders,
                TopProducts = topProducts
            };

            return ApiResponse<DashboardResponse>.SuccessResponse(response);
        }
    }
}
