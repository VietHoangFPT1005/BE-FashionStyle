using MV.DomainLayer.DTOs.Admin.Request;
using MV.DomainLayer.DTOs.Admin.Response;
using MV.DomainLayer.DTOs.Common;

namespace MV.ApplicationLayer.ServiceInterfaces
{
    public interface IAdminService
    {
        Task<ApiResponse<object>> ChangeUserRoleAsync(int adminId, int userId, ChangeRoleRequest request);
        Task<ApiResponse<object>> ChangeUserStatusAsync(int adminId, int userId, ChangeStatusRequest request);
        Task<ApiResponse<DashboardResponse>> GetDashboardAsync();
    }
}
