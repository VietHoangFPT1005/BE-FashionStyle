using MV.ApplicationLayer.ServiceInterfaces;
using MV.DomainLayer.DTOs.BodyProfile.Request;
using MV.DomainLayer.DTOs.BodyProfile.Response;
using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.Entities;
using MV.InfrastructureLayer.Interfaces;

namespace MV.ApplicationLayer.Services
{
    public class BodyProfileService : IBodyProfileService
    {
        private readonly IUserBodyProfileRepository _bodyProfileRepository;

        public BodyProfileService(IUserBodyProfileRepository bodyProfileRepository)
        {
            _bodyProfileRepository = bodyProfileRepository;
        }

        // ==================== API 13: Get Body Profile ====================
        public async Task<ApiResponse<BodyProfileResponse>> GetBodyProfileAsync(int userId)
        {
            var profile = await _bodyProfileRepository.GetByUserIdAsync(userId);

            if (profile == null)
            {
                return new ApiResponse<BodyProfileResponse>
                {
                    Success = true,
                    Message = "You have not set up your body profile yet. Please update it so AI can recommend sizes more accurately!",
                    Data = null
                };
            }

            var response = MapToResponse(profile);
            return ApiResponse<BodyProfileResponse>.SuccessResponse(response);
        }

        // ==================== API 14: Create / Update Body Profile ====================
        public async Task<ApiResponse<BodyProfileResponse>> UpsertBodyProfileAsync(int userId, UpdateBodyProfileRequest request)
        {
            var existing = await _bodyProfileRepository.GetByUserIdAsync(userId);

            if (existing == null)
            {
                // Create new
                var profile = new UserBodyProfile
                {
                    UserId = userId,
                    Height = request.Height,
                    Weight = request.Weight,
                    Bust = request.Bust,
                    Waist = request.Waist,
                    Hips = request.Hips,
                    Arm = request.Arm,
                    Thigh = request.Thigh,
                    BodyShape = request.BodyShape,
                    FitPreference = request.FitPreference ?? "Regular"
                };

                var created = await _bodyProfileRepository.CreateAsync(profile);
                var response = MapToResponse(created);
                return ApiResponse<BodyProfileResponse>.SuccessResponse(response, "Body profile created successfully.");
            }
            else
            {
                // Update existing - only update provided fields
                if (request.Height.HasValue) existing.Height = request.Height.Value;
                if (request.Weight.HasValue) existing.Weight = request.Weight.Value;
                if (request.Bust.HasValue) existing.Bust = request.Bust.Value;
                if (request.Waist.HasValue) existing.Waist = request.Waist.Value;
                if (request.Hips.HasValue) existing.Hips = request.Hips.Value;
                if (request.Arm.HasValue) existing.Arm = request.Arm.Value;
                if (request.Thigh.HasValue) existing.Thigh = request.Thigh.Value;
                if (request.BodyShape != null) existing.BodyShape = request.BodyShape;
                if (request.FitPreference != null) existing.FitPreference = request.FitPreference;

                await _bodyProfileRepository.UpdateAsync(existing);
                var response = MapToResponse(existing);
                return ApiResponse<BodyProfileResponse>.SuccessResponse(response, "Body profile updated successfully.");
            }
        }

        private static BodyProfileResponse MapToResponse(UserBodyProfile profile)
        {
            return new BodyProfileResponse
            {
                Height = profile.Height,
                Weight = profile.Weight,
                Bust = profile.Bust,
                Waist = profile.Waist,
                Hips = profile.Hips,
                Arm = profile.Arm,
                Thigh = profile.Thigh,
                BodyShape = profile.BodyShape,
                FitPreference = profile.FitPreference,
                UpdatedAt = profile.UpdatedAt
            };
        }
    }
}
