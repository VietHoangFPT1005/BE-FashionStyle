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

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }
    }
}
