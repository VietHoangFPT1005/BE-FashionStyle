using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace MV.PresentationLayer.Controllers
{
    [Route("api/health")]
    [ApiController]
    public class HealthCheckController : ControllerBase
    {
        /// <summary>
        /// Health check endpoint for uptime monitoring (UptimeRobot, etc.)
        /// </summary>
        [HttpGet("ping")]
        [HttpPost("ping")]
        [SwaggerOperation(Summary = "Health check - server is running")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [AllowAnonymous]
        public IActionResult Ping()
        {
            return Ok(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                uptime = "running"
            });
        }

        /// <summary>
        /// Simple status check (minimal response for lightweight monitoring)
        /// </summary>
        [HttpGet("status")]
        [HttpPost("status")]
        [SwaggerOperation(Summary = "Quick status check")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [AllowAnonymous]
        public IActionResult Status()
        {
            return Ok("OK");
        }
    }
}
