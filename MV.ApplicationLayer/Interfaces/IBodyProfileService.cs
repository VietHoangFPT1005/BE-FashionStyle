using MV.DomainLayer.DTOs.BodyProfile.Request;
using MV.DomainLayer.DTOs.BodyProfile.Response;
using MV.DomainLayer.DTOs.Common;

namespace MV.ApplicationLayer.ServiceInterfaces
{
    public interface IBodyProfileService
    {
        Task<ApiResponse<BodyProfileResponse>> GetBodyProfileAsync(int userId);
        Task<ApiResponse<BodyProfileResponse>> UpsertBodyProfileAsync(int userId, UpdateBodyProfileRequest request);
    }
}
