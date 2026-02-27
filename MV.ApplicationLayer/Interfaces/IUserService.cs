using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.DTOs.User.Request;
using MV.DomainLayer.DTOs.User.Response;

namespace MV.ApplicationLayer.ServiceInterfaces
{
    public interface IUserService
    {
        Task<ApiResponse<UserProfileResponse>> GetProfileAsync(int userId);
        Task<ApiResponse<UserProfileResponse>> UpdateProfileAsync(int userId, UpdateProfileRequest request);
        Task<ApiResponse<PaginatedResponse<AdminUserResponse>>> GetUsersAsync(
            int page, int pageSize, int? role, bool? isActive, string? search);
    }
}
