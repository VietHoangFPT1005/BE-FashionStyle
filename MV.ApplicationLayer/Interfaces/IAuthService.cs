using MV.DomainLayer.DTOs.Auth.Request;
using MV.DomainLayer.DTOs.Auth.Response;
using MV.DomainLayer.DTOs.Common;

namespace MV.ApplicationLayer.ServiceInterfaces
{
    public interface IAuthService
    {
        Task<ApiResponse<RegisterResponse>> RegisterAsync(RegisterRequest request);
        Task<ApiResponse<object>> VerifyEmailAsync(VerifyEmailRequest request);
        Task<ApiResponse<object>> ResendOtpAsync(ResendOtpRequest request);
        Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request);
        Task<ApiResponse<TokenResponse>> RefreshTokenAsync(RefreshTokenRequest request);
        Task<ApiResponse<object>> ForgotPasswordAsync(ForgotPasswordRequest request);
        Task<ApiResponse<object>> ResetPasswordAsync(ResetPasswordRequest request);
        Task<ApiResponse<object>> ChangePasswordAsync(int userId, ChangePasswordRequest request);
        Task<ApiResponse<object>> LogoutAsync(int userId, LogoutRequest request);
    }
}
