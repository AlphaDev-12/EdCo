using System;
using System.Threading.Tasks;
using EdCo.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EdCo.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ActivityController : ControllerBase
    {
        private readonly IAuditLogService _auditLogService;

        public ActivityController(IAuditLogService auditLogService)
        {
            _auditLogService = auditLogService;
        }

        public class LogActivityDto
        {
            public string ActivityType { get; set; } = string.Empty;
            public string? Details { get; set; }
            public string? DeviceFamily { get; set; }
        }

        [HttpPost("log")]
        [AllowAnonymous]
        public async Task<IActionResult> LogActivity([FromBody] LogActivityDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.ActivityType))
            {
                return BadRequest(new { message = "ActivityType is required." });
            }

            var studentId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var studentEmail = User.Identity?.Name;
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            await _auditLogService.LogStudentActivityAsync(
                activityType: dto.ActivityType,
                studentId: studentId,
                studentEmail: studentEmail,
                details: dto.Details,
                ipAddress: ipAddress,
                deviceFamily: dto.DeviceFamily ?? "MobileApp");

            return Ok(new { success = true });
        }
    }
}
