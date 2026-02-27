using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MV.ApplicationLayer.ServiceInterfaces;
using MV.DomainLayer.DTOs.Chat.Request;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

namespace MV.PresentationLayer.Controllers
{
    [Route("api/chat/ai")]
    [ApiController]
    [Authorize]
    public class ChatAiController : ControllerBase
    {
        private readonly IChatAiService _chatAiService;

        public ChatAiController(IChatAiService chatAiService)
        {
            _chatAiService = chatAiService;
        }

        /// <summary>
        /// Send message to AI chat for size consultation
        /// </summary>
        [HttpPost]
        [SwaggerOperation(Summary = "Send message to AI chat for size consultation")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SendMessage([FromBody] ChatMessageRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _chatAiService.SendMessageAsync(userId, request);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        /// <summary>
        /// Get list of chat sessions
        /// </summary>
        [HttpGet("sessions")]
        [SwaggerOperation(Summary = "Get list of chat sessions")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSessions()
        {
            var userId = GetCurrentUserId();
            var result = await _chatAiService.GetSessionsAsync(userId);
            return Ok(result);
        }

        /// <summary>
        /// Get chat session history
        /// </summary>
        [HttpGet("sessions/{sessionId}")]
        [SwaggerOperation(Summary = "Get chat session history")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetSessionHistory(string sessionId)
        {
            var userId = GetCurrentUserId();
            var result = await _chatAiService.GetSessionHistoryAsync(userId, sessionId);
            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }

        /// <summary>
        /// Delete a chat session
        /// </summary>
        [HttpDelete("sessions/{sessionId}")]
        [SwaggerOperation(Summary = "Delete a chat session")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteSession(string sessionId)
        {
            var userId = GetCurrentUserId();
            var result = await _chatAiService.DeleteSessionAsync(userId, sessionId);
            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }

        #region Helpers

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }

        #endregion
    }
}
