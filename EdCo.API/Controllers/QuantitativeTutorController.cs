using System;
using System.Security.Claims;
using System.Threading.Tasks;
using EdCo.API.DTOs;
using EdCo.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EdCo.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    [EnableRateLimiting("AiEndpointsPolicy")]
    public class QuantitativeTutorController : ControllerBase
    {
        private readonly ITutorEngineService _tutorService;

        public QuantitativeTutorController(ITutorEngineService tutorService)
        {
            _tutorService = tutorService;
        }

        [HttpPost("session")]
        public async Task<IActionResult> CreateSession([FromBody] CreateSessionDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            try
            {
                var session = await _tutorService.CreateSessionAsync(userId, dto.SubjectId, dto.Topic);
                return Ok(session);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("sessions/{subjectId}")]
        public async Task<IActionResult> GetSessions(int subjectId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            try
            {
                var sessions = await _tutorService.GetSessionsAsync(userId, subjectId);
                return Ok(sessions);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("session/{sessionId}")]
        public async Task<IActionResult> GetSession(Guid sessionId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            try
            {
                var session = await _tutorService.GetSessionByIdAsync(sessionId, userId);
                if (session == null)
                {
                    return NotFound();
                }
                return Ok(session);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("session/{sessionId}")]
        public async Task<IActionResult> DeleteSession(Guid sessionId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            try
            {
                var success = await _tutorService.DeleteSessionAsync(sessionId, userId);
                if (!success)
                {
                    return NotFound();
                }
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("interact")]
        public async Task<IActionResult> ProcessInteraction([FromBody] ProcessInteractionDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            try
            {
                var interaction = await _tutorService.ProcessInteractionAsync(
                    dto.SessionId, 
                    userId,
                    dto.UserMessage, 
                    dto.MathExpressionLatex, 
                    dto.UploadedImageUrl);
                    
                return Ok(interaction);
            }
            catch (Exception ex)
            {
                if (ex.Message == "QUOTA_EXCEEDED")
                {
                    return StatusCode(402, new { message = "Monthly AI usage limit reached." });
                }
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("validate-step")]
        public async Task<IActionResult> ValidateStep([FromBody] ValidateStepDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            try
            {
                var interaction = await _tutorService.ValidateStepAsync(
                    dto.SessionId, 
                    userId,
                    dto.CurrentStepLatex);
                    
                return Ok(interaction);
            }
            catch (Exception ex)
            {
                if (ex.Message == "QUOTA_EXCEEDED")
                {
                    return StatusCode(402, new { message = "Monthly AI usage limit reached." });
                }
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
