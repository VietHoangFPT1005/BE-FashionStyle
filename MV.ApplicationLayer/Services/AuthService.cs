using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MV.ApplicationLayer.ServiceInterfaces;
using MV.DomainLayer.Configuration;
using MV.DomainLayer.DTOs.Auth.Request;
using MV.DomainLayer.DTOs.Auth.Response;
using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.Entities;
using MV.InfrastructureLayer.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

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
        private readonly GoogleSettings _googleSettings;
        private readonly IHttpClientFactory _httpClientFactory;

        private const int OTP_EXPIRY_MINUTES = 5;
        private const int OTP_RATE_LIMIT = 3;
        private const int OTP_RATE_WINDOW_MINUTES = 15;
        private const int REFRESH_TOKEN_EXPIRY_DAYS = 7;
        private const string DEFAULT_AVATAR_URL = "https://antimatter.vn/wp-content/uploads/2022/11/anh-avatar-trang-tron.jpg";

        public AuthService(
            IUserRepository userRepository,
            IOtpCodeRepository otpCodeRepository,
            IRefreshTokenRepository refreshTokenRepository,
            IUserBodyProfileRepository bodyProfileRepository,
            IEmailService emailService,
            IConfiguration configuration,
            IOptions<GoogleSettings> googleSettings,
            IHttpClientFactory httpClientFactory)
        {
            _userRepository = userRepository;
            _otpCodeRepository = otpCodeRepository;
            _refreshTokenRepository = refreshTokenRepository;
            _bodyProfileRepository = bodyProfileRepository;
            _emailService = emailService;
            _configuration = configuration;
            _googleSettings = googleSettings.Value;
            _httpClientFactory = httpClientFactory;
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
            var accessTokenExpireMinutes = 10; // Fixed to 10 minutes
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
            var accessTokenExpireMinutes = 10; // Fixed to 10 minutes

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

        // ==================== API 10: Google Login ====================
        public async Task<ApiResponse<LoginResponse>> GoogleLoginAsync(GoogleLoginRequest request)
        {
            // 1. Verify Google ID Token
            GoogleJsonWebSignature.Payload payload;
            try
            {
                var settings = new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { _googleSettings.ClientId }
                };
                payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);
            }
            catch (InvalidJwtException)
            {
                return ApiResponse<LoginResponse>.ErrorResponse("Invalid Google ID token.");
            }

            // 2. Extract info from Google payload
            var email = payload.Email;
            var fullName = payload.Name;
            var avatarUrl = payload.Picture;
            var googleId = payload.Subject; // Unique Google user ID

            if (string.IsNullOrEmpty(email))
                return ApiResponse<LoginResponse>.ErrorResponse("Google account does not have an email address.");

            // 3. Check if user already exists by email
            var user = await _userRepository.GetByEmailAsync(email);

            if (user != null)
            {
                // Existing user - check if active
                if (user.IsActive != true)
                    return ApiResponse<LoginResponse>.ErrorResponse("Your account has been deactivated. Please contact support.");

                // Auto-verify email if not yet verified (Google already verified it)
                if (user.IsEmailVerified != true)
                {
                    user.IsEmailVerified = true;
                    await _userRepository.UpdateAsync(user);
                }

                // Update avatar if user doesn't have one
                if (string.IsNullOrEmpty(user.AvatarUrl))
                {
                    user.AvatarUrl = DEFAULT_AVATAR_URL;
                    await _userRepository.UpdateAsync(user);
                }
            }
            else
            {
                // 4. Create new user from Google info
                var username = $"google_{googleId}";

                // Generate a random password hash (user will never use password login)
                var randomPassword = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString(), 11);

                user = new User
                {
                    Username = username,
                    Email = email,
                    Password = randomPassword,
                    FullName = fullName,
                    AvatarUrl = DEFAULT_AVATAR_URL,
                    Role = 3, // Customer
                    IsActive = true,
                    IsEmailVerified = true // Google email is pre-verified
                };

                user = await _userRepository.CreateAsync(user);
            }

            // 5. Generate JWT + Refresh Token (same as normal login)
            var accessTokenExpireMinutes = 10; // Fixed to 10 minutes
            var accessToken = GenerateJwtToken(user, accessTokenExpireMinutes);
            var refreshTokenStr = Guid.NewGuid().ToString();

            var refreshToken = new RefreshToken
            {
                UserId = user.Id,
                Token = refreshTokenStr,
                ExpiredAt = DateTime.Now.AddDays(REFRESH_TOKEN_EXPIRY_DAYS),
                IsRevoked = false
            };
            await _refreshTokenRepository.CreateAsync(refreshToken);

            // 6. Check body profile
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
                "Google login successful.");
        }

        // ==================== API 11: Google OAuth Redirect Flow (Web/Test) ====================
        public string GetGoogleLoginUrl(string redirectUri)
        {
            var url = "https://accounts.google.com/o/oauth2/v2/auth"
                + $"?client_id={_googleSettings.ClientId}"
                + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
                + "&response_type=code"
                + "&scope=openid%20email%20profile"
                + "&access_type=offline"
                + "&prompt=consent";
            return url;
        }

        public async Task<ApiResponse<LoginResponse>> GoogleCallbackAsync(string code, string redirectUri)
        {
            // 1. Exchange authorization code for tokens
            var httpClient = _httpClientFactory.CreateClient();

            var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = _googleSettings.ClientId,
                ["client_secret"] = _googleSettings.ClientSecret,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code"
            });

            var tokenResponse = await httpClient.PostAsync("https://oauth2.googleapis.com/token", tokenRequest);
            var tokenJson = await tokenResponse.Content.ReadAsStringAsync();

            if (!tokenResponse.IsSuccessStatusCode)
                return ApiResponse<LoginResponse>.ErrorResponse($"Failed to exchange Google authorization code. {tokenJson}");

            // 2. Extract ID token from response
            using var tokenDoc = JsonDocument.Parse(tokenJson);
            var idToken = tokenDoc.RootElement.GetProperty("id_token").GetString();

            if (string.IsNullOrEmpty(idToken))
                return ApiResponse<LoginResponse>.ErrorResponse("No ID token received from Google.");

            // 3. Reuse existing GoogleLoginAsync to verify token + create/login user
            return await GoogleLoginAsync(new GoogleLoginRequest { IdToken = idToken });
        }

        // ==================== Helper Methods ====================
        private string GenerateOtp()
        {
            return RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
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
            var d1 = otpCode.Length > 0 ? otpCode[0] : '0';
            var d2 = otpCode.Length > 1 ? otpCode[1] : '0';
            var d3 = otpCode.Length > 2 ? otpCode[2] : '0';
            var d4 = otpCode.Length > 3 ? otpCode[3] : '0';
            var d5 = otpCode.Length > 4 ? otpCode[4] : '0';
            var d6 = otpCode.Length > 5 ? otpCode[5] : '0';

            var description = title switch
            {
                "Reset Your Password" => "We received a request to reset your password.<br/>Use the verification code below to proceed.",
                _ => "Thank you for choosing <strong style=\"color:#1C1C1E;\">BigSize Fashion</strong>.<br/>Enter the verification code below to secure your account."
            };

            return $@"
            <!DOCTYPE html>
            <html lang=""en"">
            <head>
                <meta charset=""UTF-8"" />
                <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
                <title>BigSize Fashion - {title}</title>
            </head>
            <body style=""margin:0;padding:0;background-color:#F5F0EB;font-family:'Segoe UI',Helvetica,Arial,sans-serif;"">
                <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#F5F0EB;padding:40px 0;"">
                    <tr>
                        <td align=""center"">
                            <table role=""presentation"" width=""600"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,0.08);"">
                    
                                <tr>
                                    <td style=""background:linear-gradient(135deg,#1C1C1E 0%,#2C2C2E 50%,#3A3A3C 100%);padding:40px 40px 35px;text-align:center;"">
                                        <h1 style=""margin:0;font-size:28px;font-weight:700;color:#D4A574;letter-spacing:6px;text-transform:uppercase;"">BIGSIZE FASHION</h1>
                                        <p style=""margin:8px 0 0;font-size:12px;color:#8E8E93;letter-spacing:3px;text-transform:uppercase;"">Your Style, Your Confidence</p>
                                    </td>
                                </tr>

                                <tr>
                                    <td style=""padding:0 40px;"">
                                        <div style=""height:3px;background:linear-gradient(90deg,#D4A574,#B76E79,#D4A574);border-radius:2px;""></div>
                                    </td>
                                </tr>

                                <tr>
                                    <td style=""padding:40px 45px 20px;text-align:center;"">
                                        <h2 style=""margin:0 0 12px;font-size:24px;font-weight:700;color:#1C1C1E;"">{title}</h2>
                                        <p style=""margin:0 0 30px;font-size:15px;color:#636366;line-height:1.7;"">
                                            {description}
                                        </p>

                                        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" align=""center"" style=""margin:0 auto 25px;"">
                                            <tr>
                                                <td style=""padding:0 4px;"">
                                                    <div style=""width:48px;height:56px;background-color:#1C1C1E;border-radius:10px;text-align:center;line-height:56px;font-size:26px;font-weight:700;color:#D4A574;"">{d1}</div>
                                                </td>
                                                <td style=""padding:0 4px;"">
                                                    <div style=""width:48px;height:56px;background-color:#1C1C1E;border-radius:10px;text-align:center;line-height:56px;font-size:26px;font-weight:700;color:#D4A574;"">{d2}</div>
                                                </td>
                                                <td style=""padding:0 4px;"">
                                                    <div style=""width:48px;height:56px;background-color:#1C1C1E;border-radius:10px;text-align:center;line-height:56px;font-size:26px;font-weight:700;color:#D4A574;"">{d3}</div>
                                                </td>
                                                <td style=""padding:0 8px;"">
                                                    <div style=""width:12px;height:4px;background-color:#D4A574;border-radius:2px;margin-top:26px;""></div>
                                                </td>
                                                <td style=""padding:0 4px;"">
                                                    <div style=""width:48px;height:56px;background-color:#1C1C1E;border-radius:10px;text-align:center;line-height:56px;font-size:26px;font-weight:700;color:#D4A574;"">{d4}</div>
                                                </td>
                                                <td style=""padding:0 4px;"">
                                                    <div style=""width:48px;height:56px;background-color:#1C1C1E;border-radius:10px;text-align:center;line-height:56px;font-size:26px;font-weight:700;color:#D4A574;"">{d5}</div>
                                                </td>
                                                <td style=""padding:0 4px;"">
                                                    <div style=""width:48px;height:56px;background-color:#1C1C1E;border-radius:10px;text-align:center;line-height:56px;font-size:26px;font-weight:700;color:#D4A574;"">{d6}</div>
                                                </td>
                                            </tr>
                                        </table>

                                        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" align=""center"" style=""margin:0 auto 30px;"">
                                            <tr>
                                                <td style=""background-color:#FFF8F0;border:1px solid #F0E0D0;border-radius:8px;padding:10px 20px;text-align:center;"">
                                                    <span style=""font-size:13px;color:#B76E79;font-weight:600;"">This code expires in {expiryMinutes} minutes</span>
                                                </td>
                                            </tr>
                                        </table>

                                        <p style=""margin:0;font-size:13px;color:#AEAEB2;line-height:1.6;"">If you didn't request this code, please safely ignore this email.</p>
                                    </td>
                                </tr>

                                <tr>
                                    <td style=""padding:0 45px;"">
                                        <div style=""height:1px;background-color:#E5E5EA;""></div>
                                    </td>
                                </tr>

                                <tr>
                                    <td style=""padding:25px 45px 30px;"">
                                        <h3 style=""margin:0 0 12px;font-size:15px;font-weight:700;color:#1C1C1E;"">Need Help?</h3>
                                        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" style=""margin-top:4px;"">
                                            <tr>
                                                <td style=""padding:4px 0;font-size:13px;color:#636366;"">
                                                    Phone: <a href=""tel:0775743304"" style=""color:#B76E79;text-decoration:none;font-weight:600;"">077 574 3304</a>
                                                </td>
                                            </tr>
                                            <tr>
                                                <td style=""padding:4px 0;font-size:13px;color:#636366;"">
                                                    Email: <a href=""mailto:hoangnv10052004@gmail.com"" style=""color:#B76E79;text-decoration:none;font-weight:600;"">hoangnv10052004@gmail.com</a>
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>

                                <tr>
                                    <td style=""background-color:#1C1C1E;padding:25px 40px;text-align:center;"">
                                        <p style=""margin:0 0 6px;font-size:12px;color:#D4A574;letter-spacing:2px;text-transform:uppercase;font-weight:600;"">BigSize Fashion</p>
                                        <p style=""margin:0;font-size:11px;color:#636366;"">&copy; 2026 BigSize Fashion. All rights reserved.</p>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                </table>
            </body>
            </html>";
        }
    }
}
