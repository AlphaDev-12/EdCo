using EdCo.API.DTOs;
using EdCo.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace EdCo.API.Controllers
{
    [ApiController]
    [Route("api/v1/ai/[controller]")]
    [Authorize]
    [EnableRateLimiting("AiEndpointsPolicy")]
    public class TutorController : ControllerBase
    {
        private readonly ITutorEngineService _tutorService;
        private readonly IAuditLogService _auditLogService;

        public TutorController(
            ITutorEngineService tutorService,
            IAuditLogService auditLogService)
        {
            _tutorService = tutorService;
            _auditLogService = auditLogService;
        }

        /// <summary>
        /// Socratic AI Tutor — quick ask endpoint.
        /// Delegates all AI communication, token tracking, and prompt engineering to ITutorEngineService.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> AskTutor([FromBody] AiTutorRequestDto request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            if (!request.SubjectId.HasValue && !request.UnitId.HasValue)
                return BadRequest("Either UnitId or SubjectId must be provided.");

            try
            {
                // Create or retrieve a session for the quick-ask flow
                var subjectId = request.SubjectId ?? 0;
                var session = await _tutorService.CreateSessionAsync(userId, subjectId, request.Message);

                var interaction = await _tutorService.ProcessInteractionAsync(
                    session.Id, userId, request.Message,
                    mathExpressionLatex: null,
                    uploadedImageUrl: null);

                await _auditLogService.LogStudentActivityAsync(
                    activityType: "AiTutorEngaged",
                    studentId: userId,
                    studentEmail: User.Identity?.Name,
                    details: $"Engaged Socratic AI Tutor for Unit #{request.UnitId} / Subject #{request.SubjectId}",
                    ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

                return Ok(new { reply = interaction.AiResponse });
            }
            catch (InvalidOperationException ex) when (ex.Message == "QUOTA_EXCEEDED")
            {
                return StatusCode(402, new { message = "Monthly AI usage limit reached." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An internal error occurred while reaching the AI Tutor." });
            }
        }
    }
}
