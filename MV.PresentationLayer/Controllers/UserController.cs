using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MV.ApplicationLayer.ServiceInterfaces;
using MV.DomainLayer.DTOs.Address.Request;
using MV.DomainLayer.DTOs.BodyProfile.Request;
using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.DTOs.User.Request;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

namespace MV.PresentationLayer.Controllers
{
    [Route("api/users")]
    [ApiController]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IBodyProfileService _bodyProfileService;
        private readonly IAddressService _addressService;

        public UserController(
            IUserService userService,
            IBodyProfileService bodyProfileService,
            IAddressService addressService)
        {
            _userService = userService;
            _bodyProfileService = bodyProfileService;
            _addressService = addressService;
        }

        // ==================== Profile APIs ====================

        /// <summary>
        /// Get current user's profile
        /// </summary>
        [HttpGet("profile")]
        [SwaggerOperation(Summary = "Get current user's profile")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetProfile()
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized(ApiResponse.ErrorResponse("Invalid token."));

            var result = await _userService.GetProfileAsync(userId);
            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }

        /// <summary>
        /// Update current user's profile
        /// </summary>
        [HttpPut("profile")]
        [SwaggerOperation(Summary = "Update current user's profile")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized(ApiResponse.ErrorResponse("Invalid token."));

            var result = await _userService.UpdateProfileAsync(userId, request);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        // ==================== Body Profile APIs ====================

        /// <summary>
        /// Get current user's body profile (Big Size feature)
        /// </summary>
        [HttpGet("body-profile")]
        [SwaggerOperation(Summary = "Get body measurements profile")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetBodyProfile()
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized(ApiResponse.ErrorResponse("Invalid token."));

            var result = await _bodyProfileService.GetBodyProfileAsync(userId);
            return Ok(result);
        }

        /// <summary>
        /// Create or update body profile (Big Size feature)
        /// </summary>
        [HttpPut("body-profile")]
        [SwaggerOperation(Summary = "Create or update body measurements profile")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpsertBodyProfile([FromBody] UpdateBodyProfileRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized(ApiResponse.ErrorResponse("Invalid token."));

            var result = await _bodyProfileService.UpsertBodyProfileAsync(userId, request);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        // ==================== Address APIs ====================

        /// <summary>
        /// Get all addresses for current user
        /// </summary>
        [HttpGet("addresses")]
        [SwaggerOperation(Summary = "Get all addresses")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetAddresses()
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized(ApiResponse.ErrorResponse("Invalid token."));

            var result = await _addressService.GetAddressesAsync(userId);
            return Ok(result);
        }

        /// <summary>
        /// Add a new address
        /// </summary>
        [HttpPost("addresses")]
        [SwaggerOperation(Summary = "Add a new shipping address")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CreateAddress([FromBody] CreateAddressRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized(ApiResponse.ErrorResponse("Invalid token."));

            var result = await _addressService.CreateAddressAsync(userId, request);
            if (!result.Success)
                return BadRequest(result);

            return StatusCode(StatusCodes.Status201Created, result);
        }

        /// <summary>
        /// Update an existing address
        /// </summary>
        [HttpPut("addresses/{addressId}")]
        [SwaggerOperation(Summary = "Update an existing address")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpdateAddress(int addressId, [FromBody] UpdateAddressRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized(ApiResponse.ErrorResponse("Invalid token."));

            var result = await _addressService.UpdateAddressAsync(userId, addressId, request);
            if (!result.Success)
            {
                if (result.Message != null && result.Message.Contains("not found"))
                    return NotFound(result);
                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Delete an address
        /// </summary>
        [HttpDelete("addresses/{addressId}")]
        [SwaggerOperation(Summary = "Delete an address")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> DeleteAddress(int addressId)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized(ApiResponse.ErrorResponse("Invalid token."));

            var result = await _addressService.DeleteAddressAsync(userId, addressId);
            if (!result.Success)
            {
                if (result.Message != null && result.Message.Contains("not found"))
                    return NotFound(result);
                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Set an address as default
        /// </summary>
        [HttpPut("addresses/{addressId}/set-default")]
        [SwaggerOperation(Summary = "Set an address as default")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> SetDefaultAddress(int addressId)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized(ApiResponse.ErrorResponse("Invalid token."));

            var result = await _addressService.SetDefaultAddressAsync(userId, addressId);
            if (!result.Success)
            {
                if (result.Message != null && result.Message.Contains("not found"))
                    return NotFound(result);
                return BadRequest(result);
            }

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
