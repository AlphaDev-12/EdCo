using System.Threading;
using EdCo.Core.Interfaces;
using EdCo.API.Services;
using EdCo.API.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EdCo.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class CurriculumController : ControllerBase
    {
        private readonly ICurriculumService _curriculumService;
        private readonly IAuditLogService _auditLogService;

        public CurriculumController(ICurriculumService curriculumService, IAuditLogService auditLogService)
        {
            _curriculumService = curriculumService;
            _auditLogService = auditLogService;
        }

        private async Task<int> GetStudentGradeLevelIdAsync(string? userId, CancellationToken ct)
        {
            var claimVal = User.FindFirst("GradeLevelId")?.Value;
            return await _curriculumService.GetStudentGradeLevelIdAsync(userId, claimVal, ct);
        }

        [HttpGet("subjects")]
        public async Task<IActionResult> GetSubjects(CancellationToken ct)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int gradeId = await GetStudentGradeLevelIdAsync(userId, ct);

            if (gradeId <= 0)
            {
                return BadRequest(new { success = false, message = "Grade level not set for this user." });
            }

            var subjects = await _curriculumService.GetSubjectsAsync(gradeId, ct);
            return Ok(subjects);
        }

        [HttpGet("subjects/{id}/manifest")]
        public async Task<IActionResult> GetSubjectManifest(int id, CancellationToken ct)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int studentGradeId = await GetStudentGradeLevelIdAsync(userId, ct);

            var chapters = await _curriculumService.GetSubjectManifestAsync(id, studentGradeId, ct);
            if (chapters == null || !chapters.Any())
            {
                return NotFound(new { success = false, message = "No manifest found for this subject or not available for your grade level." });
            }

            return Ok(chapters);
        }

        [HttpGet("subjects/{id}/exams")]
        public async Task<IActionResult> GetSubjectExams(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int studentGradeId = await GetStudentGradeLevelIdAsync(userId, ct);

            var (success, errorMsg, result) = await _curriculumService.GetSubjectExamsAsync(id, studentGradeId, page, pageSize, ct);
            if (!success)
            {
                return NotFound(new { success = false, message = errorMsg });
            }

            return Ok(result);
        }

        [HttpGet("quizzes/{id}")]
        public async Task<IActionResult> GetQuizDetails(int id, CancellationToken ct)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int studentGradeId = await GetStudentGradeLevelIdAsync(userId, ct);

            var (success, errorMsg, result) = await _curriculumService.GetQuizDetailsAsync(id, studentGradeId, userId, ct);
            if (!success)
            {
                return NotFound(new { success = false, message = errorMsg });
            }

            return Ok(result);
        }

        [HttpGet("units/{id}")]
        public async Task<IActionResult> GetUnitDetails(int id, CancellationToken ct)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int studentGradeId = await GetStudentGradeLevelIdAsync(userId, ct);
            var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";

            var (success, requiresSubscription, errorMsg, dto) = await _curriculumService.GetUnitDetailsAsync(id, studentGradeId, userId, baseUrl, ct);
            
            if (!success)
            {
                if (requiresSubscription)
                {
                    return StatusCode(403, new { success = false, requiresSubscription = true, message = errorMsg });
                }
                return NotFound(new { success = false, message = errorMsg });
            }

            await _auditLogService.LogStudentActivityAsync(
                activityType: "UnitViewed",
                studentId: userId,
                studentEmail: User.Identity?.Name,
                details: $"Viewed unit '{dto?.Title}' (Unit #{dto?.Id})",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            return Ok(dto);
        }

        [HttpGet("units/{id}/offline-questions")]
        public async Task<IActionResult> GetOfflineQuestions(int id, CancellationToken ct)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int studentGradeId = await GetStudentGradeLevelIdAsync(userId, ct);

            var (success, errorMsg, mcqQuestions) = await _curriculumService.GetOfflineQuestionsAsync(id, studentGradeId, ct);
            if (!success)
            {
                return NotFound(new { success = false, message = errorMsg });
            }

            return Ok(mcqQuestions);
        }

        [HttpGet("units/{id}/flashcards")]
        public async Task<IActionResult> GetFlashcards(int id, CancellationToken ct)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int studentGradeId = await GetStudentGradeLevelIdAsync(userId, ct);

            var (success, errorMsg, result) = await _curriculumService.GetFlashcardsAsync(id, studentGradeId, userId, ct);
            if (!success)
            {
                return NotFound(new { success = false, message = errorMsg });
            }

            return Ok(result);
        }

        [HttpPost("flashcards/{id}/master")]
        public async Task<IActionResult> MasterFlashcard(int id, CancellationToken ct)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            await _curriculumService.MasterFlashcardAsync(userId, id, ct);

            await _auditLogService.LogStudentActivityAsync(
                activityType: "FlashcardMastered",
                studentId: userId,
                studentEmail: User.Identity?.Name,
                details: $"Mastered flashcard #{id}",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            return Ok(new { success = true });
        }

        [HttpPost("quiz/submit-attempts")]
        public async Task<IActionResult> SubmitQuizAttempts([FromBody] List<QuizAttemptDto> attempts, CancellationToken ct)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            await _curriculumService.SubmitQuizAttemptsAsync(userId, attempts, ct);

            await _auditLogService.LogStudentActivityAsync(
                activityType: "QuizAttempted",
                studentId: userId,
                studentEmail: User.Identity?.Name,
                details: $"Submitted {attempts.Count} quiz question attempts ({attempts.Count(a => a.IsCorrect)} correct)",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            return Ok(new { success = true });
        }

        [HttpGet("performance")]
        public async Task<IActionResult> GetPerformance(CancellationToken ct)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            int studentGradeId = await GetStudentGradeLevelIdAsync(userId, ct);
            var result = await _curriculumService.GetPerformanceAsync(userId, studentGradeId, ct);

            return Ok(result);
        }

        [HttpPost("performance/reset")]
        public async Task<IActionResult> ResetPerformance([FromBody] ResetPerformanceDto request, CancellationToken ct)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var (success, errorMsg) = await _curriculumService.ResetPerformanceAsync(userId, request.UnitId, request.SubjectId, ct);
            if (!success)
            {
                return BadRequest(errorMsg);
            }

            return Ok(new { success = true });
        }
    }
}
