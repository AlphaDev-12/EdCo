using EdCo.API.DTOs;
using EdCo.Core.Data;
using EdCo.Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EdCo.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class SyncController : ControllerBase
    {
        private readonly EdCoDbContext _context;

        public SyncController(EdCoDbContext context)
        {
            _context = context;
        }

        [HttpPost("quiz-results")]
        public async Task<IActionResult> SyncQuizResults([FromBody] List<SyncQuizResultDto> results)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            foreach (var r in results)
            {
                var result = new QuizResult
                {
                    AppUserId = userId,
                    QuizId = r.QuizId,
                    Score = r.Score,
                    TotalQuestions = r.TotalQuestions,
                    AttemptedAt = r.AttemptedAt,
                    IsSyncedOnline = true
                };
                _context.QuizResults.Add(result);
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, count = results.Count });
        }
    }
}
