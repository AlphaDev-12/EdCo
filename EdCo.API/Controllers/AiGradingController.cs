using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using EdCo.API.DTOs;
using EdCo.API.Services;

namespace EdCo.API.Controllers
{
    [ApiController]
    [Route("api/v1/ai/[controller]")]
    [Authorize]
    [EnableRateLimiting("AiEndpointsPolicy")]
    public class GradingController : ControllerBase
    {
        private readonly IAiGradingService _gradingService;
        private readonly IAiRubricService _rubricService;
        private readonly IOcrExtractionService _ocrService;

        public GradingController(
            IAiGradingService gradingService,
            IAiRubricService rubricService,
            IOcrExtractionService ocrService)
        {
            _gradingService = gradingService;
            _rubricService = rubricService;
            _ocrService = ocrService;
        }

        [HttpPost("grade-question")]
        public async Task<IActionResult> GradeQuestion([FromBody] AiGradeRequestDto request, CancellationToken cancellationToken = default)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var (success, statusCode, errorMsg, result) = await _gradingService.GradeQuestionAsync(request, userId);

            if (!success)
            {
                if (statusCode == 444) return NotFound(errorMsg);
                if (statusCode == 400) return BadRequest(errorMsg);
                if (statusCode == 402) return StatusCode(402, new { message = errorMsg });
                if (statusCode == 429) return StatusCode(429, new { message = errorMsg });
                return StatusCode(statusCode, errorMsg);
            }

            return Ok(result);
        }

        [HttpPost("grade-question-image")]
        public async Task<IActionResult> GradeQuestionImage([FromBody] AiGradeImageRequestDto request, CancellationToken cancellationToken = default)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var (success, statusCode, errorMsg, result) = await _gradingService.GradeQuestionImageAsync(request, userId);

            if (!success)
            {
                if (statusCode == 444) return NotFound(errorMsg);
                if (statusCode == 400) return BadRequest(errorMsg);
                if (statusCode == 402) return StatusCode(402, new { message = errorMsg });
                if (statusCode == 429) return StatusCode(429, new { message = errorMsg });
                return StatusCode(statusCode, errorMsg);
            }

            return Ok(result);
        }

        [HttpPost("extract-text-from-image")]
        public async Task<IActionResult> ExtractTextFromImage([FromBody] ExtractTextRequestDto request, CancellationToken cancellationToken = default)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var (success, statusCode, errorMsg, result) = await _ocrService.ExtractTextFromImageAsync(request, userId);

            if (!success)
            {
                if (statusCode == 400) return BadRequest(errorMsg);
                if (statusCode == 429) return StatusCode(429, new { success = false, message = errorMsg });
                return StatusCode(statusCode, new { success = false, message = errorMsg });
            }

            return Ok(new
            {
                success = true,
                text = result?.Text ?? "",
                optionA = result?.OptionA ?? "",
                optionB = result?.OptionB ?? "",
                optionC = result?.OptionC ?? "",
                optionD = result?.OptionD ?? "",
                correctOption = result?.CorrectOption ?? ""
            });
        }

        [HttpPost("generate-rubric")]
        public async Task<IActionResult> GenerateRubric([FromBody] GenerateRubricRequestDto request, CancellationToken cancellationToken = default)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var (success, statusCode, errorMsg, result) = await _rubricService.GenerateRubricAsync(request, userId);

            if (!success)
            {
                if (statusCode == 400) return BadRequest(result);
                if (statusCode == 429) return StatusCode(429, result);
                return StatusCode(statusCode, result);
            }

            return Ok(result);
        }

        [HttpPost("grade-quiz-batch")]
        public async Task<IActionResult> GradeQuizBatch([FromBody] AiBatchGradeRequestDto request, CancellationToken cancellationToken = default)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var (success, statusCode, errorMsg, result) = await _gradingService.GradeQuizBatchAsync(request, userId);

            if (!success)
            {
                if (statusCode == 400) return BadRequest(errorMsg);
                if (statusCode == 402) return StatusCode(402, new { message = errorMsg });
                return StatusCode(statusCode, errorMsg);
            }

            return Ok(result);
        }

        [HttpPost("submit-quiz")]
        public async Task<IActionResult> SubmitQuizAsync([FromBody] QuizSubmissionRequestDto request, CancellationToken cancellationToken = default)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
            var (success, statusCode, errorMsg, result) = await _gradingService.SubmitQuizAsync(request, userId);

            if (!success)
            {
                if (statusCode == 400) return BadRequest(errorMsg);
                if (statusCode == 402) return StatusCode(402, new { message = errorMsg });
                return StatusCode(statusCode, errorMsg);
            }

            return StatusCode(StatusCodes.Status202Accepted, result);
        }

        [HttpGet("status/{jobId}")]
        public async Task<IActionResult> GetQuizJobStatus(string jobId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(jobId))
            {
                return BadRequest("JobId is required.");
            }

            var result = await _gradingService.GetQuizJobStatusAsync(jobId);
            if (result == null)
            {
                return NotFound("Grading job not found.");
            }

            return Ok(result);
        }
    }
}
