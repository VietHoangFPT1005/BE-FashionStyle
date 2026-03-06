using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MV.ApplicationLayer.ServiceInterfaces;
using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.DTOs.Review.Request;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

namespace MV.PresentationLayer.Controllers
{
    [Route("api/products")]
    [ApiController]
    public class ReviewController : ControllerBase
    {
        private readonly IReviewService _reviewService;

        public ReviewController(IReviewService reviewService)
        {
            _reviewService = reviewService;
        }

        /// <summary>
        /// Create product review (must have purchased and received the product)
        /// </summary>
        [HttpPost("{productId}/reviews")]
        [Authorize]
        [SwaggerOperation(Summary = "Create product review")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CreateReview(int productId, [FromBody] CreateReviewRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized(ApiResponse.ErrorResponse("Invalid token."));

            var result = await _reviewService.CreateReviewAsync(userId, productId, request);
            if (!result.Success)
            {
                if (result.Message != null && result.Message.Contains("already reviewed"))
                    return Conflict(result);
                return BadRequest(result);
            }

            return StatusCode(StatusCodes.Status201Created, result);
        }

        /// <summary>
        /// Update your review
        /// </summary>
        [HttpPut("{productId}/reviews/{reviewId}")]
        [Authorize]
        [SwaggerOperation(Summary = "Update your review")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpdateReview(int productId, int reviewId, [FromBody] UpdateReviewRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized(ApiResponse.ErrorResponse("Invalid token."));

            var result = await _reviewService.UpdateReviewAsync(userId, productId, reviewId, request);
            if (!result.Success)
            {
                if (result.Message?.Contains("not found") == true)
                    return NotFound(result);
                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Delete your review
        /// </summary>
        [HttpDelete("{productId}/reviews/{reviewId}")]
        [Authorize]
        [SwaggerOperation(Summary = "Delete your review")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> DeleteReview(int productId, int reviewId)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized(ApiResponse.ErrorResponse("Invalid token."));

            var result = await _reviewService.DeleteReviewAsync(userId, productId, reviewId);
            if (!result.Success)
                return NotFound(result);

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
