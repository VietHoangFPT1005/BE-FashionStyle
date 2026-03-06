using MV.ApplicationLayer.ServiceInterfaces;
using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.DTOs.User.Request;
using MV.DomainLayer.DTOs.User.Response;
using MV.InfrastructureLayer.Interfaces;

namespace MV.ApplicationLayer.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;

        public UserService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        // ==================== API 11: Get Profile ====================
        public async Task<ApiResponse<UserProfileResponse>> GetProfileAsync(int userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                return ApiResponse<UserProfileResponse>.ErrorResponse("User not found.");

            var response = new UserProfileResponse
            {
                UserId = user.Id,
                Username = user.Username,
                Email = user.Email ?? string.Empty,
                Phone = user.Phone,
                FullName = user.FullName ?? string.Empty,
                AvatarUrl = user.AvatarUrl,
                Gender = user.Gender,
                DateOfBirth = user.DateOfBirth,
                Role = user.Role,
                IsEmailVerified = user.IsEmailVerified == true,
                HasBodyProfile = user.UserBodyProfile != null,
                CreatedAt = user.CreatedAt
            };

            return ApiResponse<UserProfileResponse>.SuccessResponse(response);
        }

        // ==================== API 12: Update Profile ====================
        public async Task<ApiResponse<UserProfileResponse>> UpdateProfileAsync(int userId, UpdateProfileRequest request)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                return ApiResponse<UserProfileResponse>.ErrorResponse("User not found.");

            // Check phone unique if changed
            if (!string.IsNullOrEmpty(request.Phone) && request.Phone != user.Phone)
            {
                if (await _userRepository.ExistsByPhoneAsync(request.Phone))
                    return ApiResponse<UserProfileResponse>.ErrorResponse("Phone number is already registered by another user.");
            }

            // Validate date of birth
            if (request.DateOfBirth.HasValue && request.DateOfBirth.Value >= DateOnly.FromDateTime(DateTime.Now))
                return ApiResponse<UserProfileResponse>.ErrorResponse("Date of birth must be in the past.");

            // Update only provided fields (partial update)
            if (request.FullName != null) user.FullName = request.FullName;
            if (request.Phone != null) user.Phone = request.Phone;
            if (request.Gender != null) user.Gender = request.Gender;
            if (request.DateOfBirth.HasValue) user.DateOfBirth = request.DateOfBirth.Value;
            if (request.AvatarUrl != null) user.AvatarUrl = request.AvatarUrl;

            await _userRepository.UpdateAsync(user);

            var response = new UserProfileResponse
            {
                UserId = user.Id,
                Username = user.Username,
                Email = user.Email ?? string.Empty,
                Phone = user.Phone,
                FullName = user.FullName ?? string.Empty,
                AvatarUrl = user.AvatarUrl,
                Gender = user.Gender,
                DateOfBirth = user.DateOfBirth,
                Role = user.Role,
                IsEmailVerified = user.IsEmailVerified == true,
                HasBodyProfile = user.UserBodyProfile != null,
                CreatedAt = user.CreatedAt
            };

            return ApiResponse<UserProfileResponse>.SuccessResponse(response, "Profile updated successfully.");
        }

        // ==================== API 10: Get Users (Admin) ====================
        public async Task<ApiResponse<PaginatedResponse<AdminUserResponse>>> GetUsersAsync(
            int page, int pageSize, int? role, bool? isActive, string? search)
        {
            // Validate pagination params
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 50) pageSize = 50;

            var (items, totalCount) = await _userRepository.GetUsersPagedAsync(
                page, pageSize, role, isActive, search);

            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            var userResponses = items.Select(u => new AdminUserResponse
            {
                UserId = u.Id,
                Username = u.Username,
                Email = u.Email ?? string.Empty,
                Phone = u.Phone,
                FullName = u.FullName ?? string.Empty,
                Gender = u.Gender,
                Role = u.Role,
                IsActive = u.IsActive == true,
                IsEmailVerified = u.IsEmailVerified == true,
                CreatedAt = u.CreatedAt
            }).ToList();

            var paginatedResponse = new PaginatedResponse<AdminUserResponse>
            {
                Items = userResponses,
                Pagination = new PaginationInfo
                {
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalItems = totalCount,
                    TotalPages = totalPages,
                    HasNext = page < totalPages,
                    HasPrevious = page > 1
                }
            };

            return ApiResponse<PaginatedResponse<AdminUserResponse>>.SuccessResponse(paginatedResponse);
        }
    }
}
