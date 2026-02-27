using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MV.ApplicationLayer.ServiceInterfaces;
using MV.DomainLayer.DTOs.Auth.Request;
using MV.DomainLayer.DTOs.Auth.Response;
using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.Entities;
using MV.InfrastructureLayer.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace MV.ApplicationLayer.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IOtpCodeRepository _otpCodeRepository;
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly IUserBodyProfileRepository _bodyProfileRepository;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;

        private const int OTP_EXPIRY_MINUTES = 5;
        private const int OTP_RATE_LIMIT = 3;
        private const int OTP_RATE_WINDOW_MINUTES = 15;
        private const int REFRESH_TOKEN_EXPIRY_DAYS = 7;

        public AuthService(
            IUserRepository userRepository,
            IOtpCodeRepository otpCodeRepository,
            IRefreshTokenRepository refreshTokenRepository,
            IUserBodyProfileRepository bodyProfileRepository,
            IEmailService emailService,
            IConfiguration configuration)
        {
            _userRepository = userRepository;
            _otpCodeRepository = otpCodeRepository;
            _refreshTokenRepository = refreshTokenRepository;
            _bodyProfileRepository = bodyProfileRepository;
            _emailService = emailService;
            _configuration = configuration;
        }

        // ==================== API 1: Register ====================
        public async Task<ApiResponse<RegisterResponse>> RegisterAsync(RegisterRequest request)
        {
            // Check unique constraints
            if (await _userRepository.ExistsByUsernameAsync(request.Username))
                return ApiResponse<RegisterResponse>.ErrorResponse("Username is already taken.");

            if (await _userRepository.ExistsByEmailAsync(request.Email))
                return ApiResponse<RegisterResponse>.ErrorResponse("Email is already registered.");

            if (!string.IsNullOrEmpty(request.Phone) && await _userRepository.ExistsByPhoneAsync(request.Phone))
                return ApiResponse<RegisterResponse>.ErrorResponse("Phone number is already registered.");

            // Hash password with BCrypt (cost factor 11)
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password, 11);

            // Create user
            var user = new User
            {
                Username = request.Username,
                Email = request.Email,
                Phone = request.Phone,
                Password = hashedPassword,
                FullName = request.FullName,
                Gender = request.Gender,
                Role = 3, // Customer
                IsActive = true,
                IsEmailVerified = false
            };

            var createdUser = await _userRepository.CreateAsync(user);

            // Generate and save OTP
            var otpCode = GenerateOtp();
            var otp = new OtpCode
            {
                UserId = createdUser.Id,
                Email = request.Email,
                Code = otpCode,
                Type = "VERIFY_EMAIL",
                ExpiredAt = DateTime.Now.AddMinutes(OTP_EXPIRY_MINUTES),
                IsUsed = false
            };
            await _otpCodeRepository.CreateAsync(otp);

            // Send OTP email
            var htmlBody = BuildOtpEmailTemplate(otpCode, "Verify Your Account", OTP_EXPIRY_MINUTES);
            await _emailService.SendEmailAsync(request.Email, "Email Verification - OTP Code", htmlBody);

            return ApiResponse<RegisterResponse>.SuccessResponse(
                new RegisterResponse
                {
                    UserId = createdUser.Id,
                    Email = request.Email,
                    RequiresVerification = true
                },
                "Registration successful. Please verify your email with the OTP code sent.");
        }

        // ==================== API 2: Verify Email ====================
        public async Task<ApiResponse<object>> VerifyEmailAsync(VerifyEmailRequest request)
        {
            var otp = await _otpCodeRepository.GetValidOtpAsync(request.Email, "VERIFY_EMAIL");

            if (otp == null)
                return ApiResponse<object>.ErrorResponse("No valid OTP found. Please request a new one.");

            if (otp.Code != request.OtpCode)
                return ApiResponse<object>.ErrorResponse("Invalid OTP code.");

            if (otp.ExpiredAt < DateTime.Now)
                return ApiResponse<object>.ErrorResponse("OTP code has expired. Please request a new one.");

            // Mark OTP as used
            await _otpCodeRepository.MarkAsUsedAsync(otp.Id);

            // Update user email verified status
            var user = await _userRepository.GetByEmailAsync(request.Email);
            if (user != null)
            {
                user.IsEmailVerified = true;
                await _userRepository.UpdateAsync(user);
            }

            return ApiResponse<object>.SuccessResponse(null, "Email verified successfully. You can now log in.");
        }

        // ==================== API 3: Resend OTP ====================
        public async Task<ApiResponse<object>> ResendOtpAsync(ResendOtpRequest request)
        {
            var user = await _userRepository.GetByEmailAsync(request.Email);
            if (user == null)
                return ApiResponse<object>.ErrorResponse("Email does not exist in the system.");

            // Rate limit check
            var recentCount = await _otpCodeRepository.CountRecentOtpAsync(
                request.Email, request.Type, OTP_RATE_WINDOW_MINUTES);

            if (recentCount >= OTP_RATE_LIMIT)
                return ApiResponse<object>.ErrorResponse(
                    $"You have exceeded the maximum of {OTP_RATE_LIMIT} OTP requests. Please try again after {OTP_RATE_WINDOW_MINUTES} minutes.");

            // Invalidate old unused OTPs
            await _otpCodeRepository.InvalidateAllOtpAsync(request.Email, request.Type);

            // Generate new OTP
            var otpCode = GenerateOtp();
            var otp = new OtpCode
            {
                UserId = user.Id,
                Email = request.Email,
                Code = otpCode,
                Type = request.Type,
                ExpiredAt = DateTime.Now.AddMinutes(OTP_EXPIRY_MINUTES),
                IsUsed = false
            };
            await _otpCodeRepository.CreateAsync(otp);

            // Send email
            var subject = request.Type == "VERIFY_EMAIL"
                ? "Email Verification - OTP Code"
                : "Password Reset - OTP Code";
            var title = request.Type == "VERIFY_EMAIL"
                ? "Verify Your Account"
                : "Reset Your Password";

            var htmlBody = BuildOtpEmailTemplate(otpCode, title, OTP_EXPIRY_MINUTES);
            await _emailService.SendEmailAsync(request.Email, subject, htmlBody);

            return ApiResponse<object>.SuccessResponse(null, "A new OTP code has been sent to your email.");
        }

        // ==================== API 4: Login ====================
        public async Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request)
        {
            var user = await _userRepository.GetByEmailOrPhoneAsync(request.EmailOrPhone);

            if (user == null)
                return ApiResponse<LoginResponse>.ErrorResponse("Invalid email/phone or password.");

            if (user.IsActive != true)
                return ApiResponse<LoginResponse>.ErrorResponse("Your account has been deactivated. Please contact support.");

            if (user.IsEmailVerified != true)
            {
                var response = new ApiResponse<LoginResponse>
                {
                    Success = false,
                    Message = "Your email has not been verified. Please check your inbox.",
                    Data = new LoginResponse
                    {
                        AccessToken = string.Empty,
                        RefreshToken = string.Empty,
                        ExpiresIn = 0,
                        User = new UserInfoResponse
                        {
                            UserId = user.Id,
                            Email = user.Email ?? string.Empty,
                            Username = user.Username,
                            FullName = user.FullName ?? string.Empty,
                            Role = user.Role,
                            IsEmailVerified = false,
                            HasBodyProfile = false
                        }
                    }
                };
                return response;
            }

            // Verify password
            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
                return ApiResponse<LoginResponse>.ErrorResponse("Invalid email/phone or password.");

            // Generate tokens
            var accessTokenExpireMinutes = int.Parse(_configuration["Jwt:ExpireMinutes"] ?? "60");
            var accessToken = GenerateJwtToken(user, accessTokenExpireMinutes);
            var refreshTokenStr = Guid.NewGuid().ToString();

            // Save refresh token
            var refreshToken = new RefreshToken
            {
                UserId = user.Id,
                Token = refreshTokenStr,
                ExpiredAt = DateTime.Now.AddDays(REFRESH_TOKEN_EXPIRY_DAYS),
                IsRevoked = false
            };
            await _refreshTokenRepository.CreateAsync(refreshToken);

            // Check body profile
            var hasBodyProfile = user.UserBodyProfile != null;

            return ApiResponse<LoginResponse>.SuccessResponse(
                new LoginResponse
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshTokenStr,
                    ExpiresIn = accessTokenExpireMinutes * 60,
                    User = new UserInfoResponse
                    {
                        UserId = user.Id,
                        Username = user.Username,
                        FullName = user.FullName ?? string.Empty,
                        Email = user.Email ?? string.Empty,
                        AvatarUrl = user.AvatarUrl,
                        Gender = user.Gender,
                        Role = user.Role,
                        IsEmailVerified = user.IsEmailVerified == true,
                        HasBodyProfile = hasBodyProfile
                    }
                },
                "Login successful.");
        }

        // ==================== API 5: Refresh Token ====================
        public async Task<ApiResponse<TokenResponse>> RefreshTokenAsync(RefreshTokenRequest request)
        {
            var storedToken = await _refreshTokenRepository.GetByTokenAsync(request.RefreshToken);

            if (storedToken == null || storedToken.IsRevoked == true || storedToken.ExpiredAt < DateTime.Now)
                return ApiResponse<TokenResponse>.ErrorResponse("Invalid or expired refresh token.");

            // Revoke old token
            await _refreshTokenRepository.RevokeAsync(storedToken.Id);

            var user = storedToken.User;
            var accessTokenExpireMinutes = int.Parse(_configuration["Jwt:ExpireMinutes"] ?? "60");

            // Generate new tokens
            var newAccessToken = GenerateJwtToken(user, accessTokenExpireMinutes);
            var newRefreshTokenStr = Guid.NewGuid().ToString();

            var newRefreshToken = new RefreshToken
            {
                UserId = user.Id,
                Token = newRefreshTokenStr,
                ExpiredAt = DateTime.Now.AddDays(REFRESH_TOKEN_EXPIRY_DAYS),
                IsRevoked = false
            };
            await _refreshTokenRepository.CreateAsync(newRefreshToken);

            return ApiResponse<TokenResponse>.SuccessResponse(
                new TokenResponse
                {
                    AccessToken = newAccessToken,
                    RefreshToken = newRefreshTokenStr,
                    ExpiresIn = accessTokenExpireMinutes * 60
                });
        }

        // ==================== API 6: Forgot Password ====================
        public async Task<ApiResponse<object>> ForgotPasswordAsync(ForgotPasswordRequest request)
        {
            var user = await _userRepository.GetByEmailAsync(request.Email);
            if (user == null)
                return ApiResponse<object>.ErrorResponse("Email does not exist in the system.");

            // Rate limit
            var recentCount = await _otpCodeRepository.CountRecentOtpAsync(
                request.Email, "RESET_PASSWORD", OTP_RATE_WINDOW_MINUTES);

            if (recentCount >= OTP_RATE_LIMIT)
                return ApiResponse<object>.ErrorResponse(
                    $"You have exceeded the maximum of {OTP_RATE_LIMIT} OTP requests. Please try again after {OTP_RATE_WINDOW_MINUTES} minutes.");

            // Invalidate old OTPs
            await _otpCodeRepository.InvalidateAllOtpAsync(request.Email, "RESET_PASSWORD");

            // Generate new OTP
            var otpCode = GenerateOtp();
            var otp = new OtpCode
            {
                UserId = user.Id,
                Email = request.Email,
                Code = otpCode,
                Type = "RESET_PASSWORD",
                ExpiredAt = DateTime.Now.AddMinutes(OTP_EXPIRY_MINUTES),
                IsUsed = false
            };
            await _otpCodeRepository.CreateAsync(otp);

            var htmlBody = BuildOtpEmailTemplate(otpCode, "Reset Your Password", OTP_EXPIRY_MINUTES);
            await _emailService.SendEmailAsync(request.Email, "Password Reset - OTP Code", htmlBody);

            return ApiResponse<object>.SuccessResponse(null, "A password reset OTP has been sent to your email.");
        }

        // ==================== API 7: Reset Password ====================
        public async Task<ApiResponse<object>> ResetPasswordAsync(ResetPasswordRequest request)
        {
            // Verify OTP
            var otp = await _otpCodeRepository.GetValidOtpAsync(request.Email, "RESET_PASSWORD");

            if (otp == null)
                return ApiResponse<object>.ErrorResponse("No valid OTP found. Please request a new one.");

            if (otp.Code != request.OtpCode)
                return ApiResponse<object>.ErrorResponse("Invalid OTP code.");

            if (otp.ExpiredAt < DateTime.Now)
                return ApiResponse<object>.ErrorResponse("OTP code has expired. Please request a new one.");

            // Mark OTP as used
            await _otpCodeRepository.MarkAsUsedAsync(otp.Id);

            // Update password
            var user = await _userRepository.GetByEmailAsync(request.Email);
            if (user == null)
                return ApiResponse<object>.ErrorResponse("User not found.");

            user.Password = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, 11);
            await _userRepository.UpdateAsync(user);

            // Revoke all refresh tokens for security
            await _refreshTokenRepository.RevokeAllByUserIdAsync(user.Id);

            return ApiResponse<object>.SuccessResponse(null, "Password reset successful. Please log in with your new password.");
        }

        // ==================== API 8: Change Password ====================
        public async Task<ApiResponse<object>> ChangePasswordAsync(int userId, ChangePasswordRequest request)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                return ApiResponse<object>.ErrorResponse("User not found.");

            // Verify current password
            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.Password))
                return ApiResponse<object>.ErrorResponse("Current password is incorrect.");

            // Check new password != current password
            if (request.CurrentPassword == request.NewPassword)
                return ApiResponse<object>.ErrorResponse("New password must be different from the current password.");

            // Verify OTP
            var otp = await _otpCodeRepository.GetValidOtpAsync(user.Email!, "RESET_PASSWORD");
            if (otp == null || otp.Code != request.OtpCode)
                return ApiResponse<object>.ErrorResponse("Invalid or expired OTP code.");

            // Mark OTP used
            await _otpCodeRepository.MarkAsUsedAsync(otp.Id);

            // Update password
            user.Password = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, 11);
            await _userRepository.UpdateAsync(user);

            // Revoke all refresh tokens
            await _refreshTokenRepository.RevokeAllByUserIdAsync(userId);

            return ApiResponse<object>.SuccessResponse(null, "Password changed successfully.");
        }

        // ==================== API 9: Logout ====================
        public async Task<ApiResponse<object>> LogoutAsync(int userId, LogoutRequest request)
        {
            var token = await _refreshTokenRepository.GetByTokenAsync(request.RefreshToken);

            if (token != null && token.UserId == userId && token.IsRevoked != true)
            {
                await _refreshTokenRepository.RevokeAsync(token.Id);
            }

            return ApiResponse<object>.SuccessResponse(null, "Logged out successfully.");
        }

        // ==================== Helper Methods ====================
        private string GenerateOtp()
        {
            var random = new Random();
            return random.Next(100000, 999999).ToString();
        }

        private string GenerateJwtToken(User user, int expireMinutes)
        {
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                new Claim("userId", user.Id.ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddMinutes(expireMinutes),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string BuildOtpEmailTemplate(string otpCode, string title, int expiryMinutes)
        {
            return $@"
            <!DOCTYPE html>
            <html lang=""en"">
            <head>
                <meta charset=""UTF-8"" />
                <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
                <title>BigSize Fashion - {title}</title>
                <style>
                    body {{
                        margin: 0; padding: 0;
                        background-color: #f8f9fa;
                        font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                    }}
                    .container {{
                        max-width: 600px; margin: 30px auto;
                        background-color: #ffffff; border-radius: 12px;
                        box-shadow: 0 6px 20px rgba(0,0,0,0.05);
                        overflow: hidden; border: 1px solid #dee2e6;
                    }}
                    .header {{
                        background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
                        padding: 30px 20px;
                        text-align: center; color: white;
                    }}
                    .header h1 {{ margin: 0; font-size: 28px; font-weight: 700; }}
                    .content {{ padding: 40px 45px; color: #343a40; line-height: 1.6; }}
                    .content h2 {{
                        font-size: 24px; font-weight: 700;
                        margin: 0 0 15px 0; text-align: center; color: #667eea;
                    }}
                    .content p {{
                        font-size: 16px; color: #495057;
                        text-align: center; margin-bottom: 25px;
                    }}
                    .otp-box {{
                        background-color: #f0f0ff; border-radius: 8px;
                        padding: 15px 20px; font-size: 36px; font-weight: 700;
                        color: #667eea; letter-spacing: 8px; text-align: center;
                        margin: 20px auto 30px; border: 1px solid #e0e0ff;
                    }}
                    .expiry {{ font-size: 14px; color: #6c757d; text-align: center; }}
                    .footer {{
                        background-color: #f1f3f5; padding: 30px;
                        text-align: center; font-size: 13px; color: #6c757d;
                    }}
                </style>
            </head>
            <body>
                <div class=""container"">
                    <div class=""header"">
                        <h1>BigSize Fashion</h1>
                    </div>
                    <div class=""content"">
                        <h2>{title}</h2>
                        <p>Please use the code below to complete your request.</p>
                        <div class=""otp-box"">{otpCode}</div>
                        <p class=""expiry"">This code is valid for <strong>{expiryMinutes} minutes</strong>.</p>
                    </div>
                    <div class=""footer"">
                        <p>&copy; 2026 BigSize Fashion. All rights reserved.</p>
                    </div>
                </div>
            </body>
            </html>";
        }
    }
}
