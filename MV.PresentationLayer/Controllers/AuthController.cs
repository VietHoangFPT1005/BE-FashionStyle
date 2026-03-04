using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MV.ApplicationLayer.ServiceInterfaces;
using MV.DomainLayer.DTOs.Auth.Request;
using MV.DomainLayer.DTOs.Common;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

namespace MV.PresentationLayer.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// Register a new customer account
        /// </summary>
        [HttpPost("register")]
        [SwaggerOperation(Summary = "Register a new customer account")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var result = await _authService.RegisterAsync(request);
            if (!result.Success)
                return BadRequest(result);

            return StatusCode(StatusCodes.Status201Created, result);
        }

        /// <summary>
        /// Verify email with OTP code
        /// </summary>
        [HttpPost("verify-email")]
        [SwaggerOperation(Summary = "Verify email with OTP code")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
        {
            var result = await _authService.VerifyEmailAsync(request);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        /// <summary>
        /// Resend OTP code to email
        /// </summary>
        [HttpPost("resend-otp")]
        [SwaggerOperation(Summary = "Resend OTP code to email")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> ResendOtp([FromBody] ResendOtpRequest request)
        {
            var result = await _authService.ResendOtpAsync(request);
            if (!result.Success)
            {
                if (result.Message != null && result.Message.Contains("exceeded"))
                    return StatusCode(StatusCodes.Status429TooManyRequests, result);
                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Login with email/phone and password
        /// </summary>
        [HttpPost("login")]
        [SwaggerOperation(Summary = "Login with email/phone and password")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var result = await _authService.LoginAsync(request);
            if (!result.Success)
            {
                if (result.Message != null && (result.Message.Contains("deactivated") || result.Message.Contains("not been verified")))
                    return StatusCode(StatusCodes.Status403Forbidden, result);
                return Unauthorized(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Refresh access token using refresh token
        /// </summary>
        [HttpPost("refresh-token")]
        [SwaggerOperation(Summary = "Refresh access token")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            var result = await _authService.RefreshTokenAsync(request);
            if (!result.Success)
                return Unauthorized(result);

            return Ok(result);
        }

        /// <summary>
        /// Send OTP to reset forgotten password
        /// </summary>
        [HttpPost("forgot-password")]
        [SwaggerOperation(Summary = "Send OTP to reset forgotten password")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            var result = await _authService.ForgotPasswordAsync(request);
            if (!result.Success)
            {
                if (result.Message != null && result.Message.Contains("does not exist"))
                    return NotFound(result);
                if (result.Message != null && result.Message.Contains("exceeded"))
                    return StatusCode(StatusCodes.Status429TooManyRequests, result);
                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Reset password with OTP verification
        /// </summary>
        [HttpPost("reset-password")]
        [SwaggerOperation(Summary = "Reset password with OTP verification")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            var result = await _authService.ResetPasswordAsync(request);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        /// <summary>
        /// Change password (requires current password and OTP)
        /// </summary>
        [HttpPut("change-password")]
        [Authorize]
        [SwaggerOperation(Summary = "Change password (requires authentication)")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized(ApiResponse.ErrorResponse("Invalid token."));

            var result = await _authService.ChangePasswordAsync(userId, request);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        /// <summary>
        /// Login or register with Google account
        /// </summary>
        [HttpPost("google-login")]
        [SwaggerOperation(Summary = "Login or register with Google account")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
        {
            var result = await _authService.GoogleLoginAsync(request);
            if (!result.Success)
            {
                if (result.Message != null && result.Message.Contains("deactivated"))
                    return StatusCode(StatusCodes.Status403Forbidden, result);
                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Redirect to Google login page (open in browser to test)
        /// </summary>
        [HttpGet("google-redirect")]
        [SwaggerOperation(Summary = "Redirect to Google login page (open URL in browser)")]
        [ProducesResponseType(StatusCodes.Status302Found)]
        public IActionResult GoogleRedirect()
        {
            var redirectUri = $"{Request.Scheme}://{Request.Host}/api/auth/google-callback";
            var googleLoginUrl = _authService.GetGoogleLoginUrl(redirectUri);
            return Redirect(googleLoginUrl);
        }

        /// <summary>
        /// Google OAuth callback - handles the response from Google after login
        /// </summary>
        [HttpGet("google-callback")]
        [SwaggerOperation(Summary = "Google OAuth callback (auto-called by Google after login)")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GoogleCallback([FromQuery] string code, [FromQuery] string? error)
        {
            if (!string.IsNullOrEmpty(error))
                return BadRequest(ApiResponse.ErrorResponse($"Google login was denied: {error}"));

            if (string.IsNullOrEmpty(code))
                return BadRequest(ApiResponse.ErrorResponse("Authorization code is missing."));

            var redirectUri = $"{Request.Scheme}://{Request.Host}/api/auth/google-callback";
            var result = await _authService.GoogleCallbackAsync(code, redirectUri);

            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        /// <summary>
        /// Logout and revoke refresh token
        /// </summary>
        [HttpPost("logout")]
        [Authorize]
        [SwaggerOperation(Summary = "Logout and revoke refresh token")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized(ApiResponse.ErrorResponse("Invalid token."));

            var result = await _authService.LogoutAsync(userId, request);
            return Ok(result);
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }
    }
}
