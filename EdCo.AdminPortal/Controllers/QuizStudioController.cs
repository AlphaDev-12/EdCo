using EdCo.Core.Data;
using EdCo.Core.Entities;
using EdCo.AdminPortal.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Microsoft.AspNetCore.Authorization;

namespace EdCo.AdminPortal.Controllers
{
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class QuizStudioController : Controller
    {
        private readonly EdCoDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly EdCo.Core.Interfaces.IAuditLogService _auditLogService;
        private readonly EdCo.Core.Interfaces.IErrorLogService _errorLogService;

        public QuizStudioController(
            EdCoDbContext context, 
            IConfiguration configuration, 
            IHttpClientFactory httpClientFactory,
            EdCo.Core.Interfaces.IAuditLogService auditLogService,
            EdCo.Core.Interfaces.IErrorLogService errorLogService)
        {
            _context = context;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _auditLogService = auditLogService;
            _errorLogService = errorLogService;
        }

        private string GetCurrentUserId() => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
        private string GetCurrentUserName() => User.Identity?.Name ?? "Admin";
        private string GetCurrentUserRole() => User.IsInRole("SuperAdmin") ? "SuperAdmin" : "Admin";
        private string GetClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

        public async Task<IActionResult> Index()
        {
            var quizzes = await _context.Quizzes
                .Include(q => q.Unit)
                    .ThenInclude(u => u.Chapter)
                        .ThenInclude(c => c.Subject)
                            .ThenInclude(s => s.GradeLevel)
                .Include(q => q.Subject)
                    .ThenInclude(s => s.GradeLevel)
                .Include(q => q.Questions)
                .OrderBy(q => q.Unit != null && q.Unit.Chapter != null && q.Unit.Chapter.Subject != null && q.Unit.Chapter.Subject.GradeLevel != null ? q.Unit.Chapter.Subject.GradeLevel.Name : (q.Subject != null && q.Subject.GradeLevel != null ? q.Subject.GradeLevel.Name : ""))
                .ToListAsync();
                
            ViewBag.Subjects = await _context.Subjects
                .Include(s => s.GradeLevel)
                .OrderBy(s => s.GradeLevel.Name).ThenBy(s => s.Name)
                .ToListAsync();
                
            return View(quizzes);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var quiz = await _context.Quizzes
                .Include(q => q.Unit)
                    .ThenInclude(u => u.Chapter)
                        .ThenInclude(c => c.Subject)
                            .ThenInclude(s => s.GradeLevel)
                .Include(q => q.Subject)
                    .ThenInclude(s => s.GradeLevel)
                .Include(q => q.Questions)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (quiz == null) return NotFound();
            return View(quiz);
        }

        // POST: /QuizStudio/CreateForUnit — Create quiz for a unit (called from Builder)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateForUnit([FromBody] CreateQuizRequest request)
        {
            var existing = await _context.Quizzes.FirstOrDefaultAsync(q => q.UnitId == request.UnitId);
            if (existing != null)
            {
                return Json(new { success = false, message = "Quiz already exists for this unit.", quizId = existing.Id });
            }

            var quiz = new Quiz
            {
                UnitId = request.UnitId,
                Title = request.Title
            };
            _context.Quizzes.Add(quiz);
            await _context.SaveChangesAsync();

            await _auditLogService.LogAdminActionAsync(
                action: "CreateQuiz",
                entityName: "Quiz",
                entityId: quiz.Id.ToString(),
                details: $"Created quiz '{request.Title}' for unit #{request.UnitId}",
                userId: GetCurrentUserId(),
                userName: GetCurrentUserName(),
                userRole: GetCurrentUserRole(),
                ipAddress: GetClientIp());

            return Json(new { success = true, quizId = quiz.Id });
        }

        // POST: /QuizStudio/AddQuestion
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddQuestion([FromBody] AddQuestionRequest request)
        {
            var question = new QuizQuestion
            {
                QuizId = request.QuizId,
                QuestionText = request.QuestionText,
                QuestionType = request.QuestionType,
                Points = request.Points,
                OptionA = request.OptionA,
                OptionB = request.OptionB,
                OptionC = request.OptionC,
                OptionD = request.OptionD,
                CorrectOption = request.CorrectOption,
                CorrectAnswer = request.CorrectAnswer,
                CorrectAnswerImageUrl = request.CorrectAnswerImageUrl,
                RubricJson = request.RubricJson,
                ImageUrl = request.ImageUrl
            };
            _context.QuizQuestions.Add(question);
            await _context.SaveChangesAsync();

            await _auditLogService.LogAdminActionAsync(
                action: "AddQuestion",
                entityName: "QuizQuestion",
                entityId: question.Id.ToString(),
                details: $"Added question to quiz #{request.QuizId}: '{request.QuestionText.Substring(0, Math.Min(30, request.QuestionText.Length))}...'",
                userId: GetCurrentUserId(),
                userName: GetCurrentUserName(),
                userRole: GetCurrentUserRole(),
                ipAddress: GetClientIp());

            return Json(new { success = true, id = question.Id });
        }

        // POST: /QuizStudio/UpdateQuestion
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateQuestion([FromBody] UpdateQuestionRequest request)
        {
            var question = await _context.QuizQuestions.FindAsync(request.Id);
            if (question == null) return NotFound();

            question.QuestionText = request.QuestionText;
            question.QuestionType = request.QuestionType;
            question.Points = request.Points;
            question.OptionA = request.OptionA;
            question.OptionB = request.OptionB;
            question.OptionC = request.OptionC;
            question.OptionD = request.OptionD;
            question.CorrectOption = request.CorrectOption;
            question.CorrectAnswer = request.CorrectAnswer;
            question.RubricJson = request.RubricJson;
            if (request.CorrectAnswerImageUrl != null)
            {
                question.CorrectAnswerImageUrl = string.IsNullOrWhiteSpace(request.CorrectAnswerImageUrl) ? null : request.CorrectAnswerImageUrl;
            }
            if (request.ImageUrl != null)
            {
                question.ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl;
            }

            await _context.SaveChangesAsync();

            await _auditLogService.LogAdminActionAsync(
                action: "UpdateQuestion",
                entityName: "QuizQuestion",
                entityId: question.Id.ToString(),
                details: $"Updated question #{question.Id} in quiz #{question.QuizId}: '{(request.QuestionText.Length > 30 ? request.QuestionText.Substring(0, 30) + "..." : request.QuestionText)}'",
                userId: GetCurrentUserId(),
                userName: GetCurrentUserName(),
                userRole: GetCurrentUserRole(),
                ipAddress: GetClientIp());

            return Json(new { success = true, id = question.Id });
        }

        // POST: /QuizStudio/DeleteQuestion
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteQuestion(int id)
        {
            var question = await _context.QuizQuestions.FindAsync(id);
            if (question != null)
            {
                question.DeletedBy = GetCurrentUserName();
                _context.QuizQuestions.Remove(question);
                await _context.SaveChangesAsync();

                await _auditLogService.LogAdminActionAsync(
                    action: "DeleteQuestion",
                    entityName: "QuizQuestion",
                    entityId: id.ToString(),
                    details: $"Soft deleted question #{id} from quiz #{question.QuizId}",
                    userId: GetCurrentUserId(),
                    userName: GetCurrentUserName(),
                    userRole: GetCurrentUserRole(),
                    ipAddress: GetClientIp());
            }
            return Json(new { success = true });
        }

        // POST: /QuizStudio/DeleteQuiz
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteQuiz(int id)
        {
            var quiz = await _context.Quizzes.FindAsync(id);
            if (quiz != null)
            {
                quiz.DeletedBy = GetCurrentUserName();
                _context.Quizzes.Remove(quiz);
                await _context.SaveChangesAsync();

                await _auditLogService.LogAdminActionAsync(
                    action: "DeleteQuiz",
                    entityName: "Quiz",
                    entityId: id.ToString(),
                    details: $"Soft deleted quiz '{quiz.Title}' (Id: {id})",
                    userId: GetCurrentUserId(),
                    userName: GetCurrentUserName(),
                    userRole: GetCurrentUserRole(),
                    ipAddress: GetClientIp());

                TempData["Success"] = $"Quiz '{quiz.Title}' deleted (soft-delete).";
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: /QuizStudio/UpdateTitle
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateTitle([FromBody] UpdateTitleRequest request)
        {
            var quiz = await _context.Quizzes.FindAsync(request.QuizId);
            if (quiz == null) return NotFound();

            quiz.Title = request.Title;
            await _context.SaveChangesAsync();

            await _auditLogService.LogAdminActionAsync(
                action: "UpdateQuizTitle",
                entityName: "Quiz",
                entityId: quiz.Id.ToString(),
                details: $"Updated quiz title to '{request.Title}'",
                userId: GetCurrentUserId(),
                userName: GetCurrentUserName(),
                userRole: GetCurrentUserRole(),
                ipAddress: GetClientIp());

            return Json(new { success = true });
        }

        // POST: /QuizStudio/CreateExamForSubject
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateExamForSubject([FromBody] CreateExamRequest request)
        {
            var quiz = new Quiz
            {
                SubjectId = request.SubjectId,
                Title = request.Title,
                IsExam = true,
                DisplayPosition = 0
            };
            _context.Quizzes.Add(quiz);
            await _context.SaveChangesAsync();

            await _auditLogService.LogAdminActionAsync(
                action: "CreateExam",
                entityName: "Quiz",
                entityId: quiz.Id.ToString(),
                details: $"Created exam '{request.Title}' for subject #{request.SubjectId}",
                userId: GetCurrentUserId(),
                userName: GetCurrentUserName(),
                userRole: GetCurrentUserRole(),
                ipAddress: GetClientIp());

            return Json(new { success = true, quizId = quiz.Id });
        }

        // POST: /QuizStudio/UpdateExamStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateExamStatus([FromBody] UpdateExamStatusRequest request)
        {
            var quiz = await _context.Quizzes.FindAsync(request.QuizId);
            if (quiz == null) return NotFound();

            quiz.DisplayPosition = request.DisplayPosition;
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        // POST: /QuizStudio/ExtractTextFromImage
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(30_000_000)] // 30MB for large camera images
        public async Task<IActionResult> ExtractTextFromImage([FromBody] ExtractTextRequest request, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(request.Base64Image))
            {
                return Json(new { success = false, message = "No image data provided." });
            }

            try
            {
                var client = _httpClientFactory.CreateClient("EdCoApi");
                var apiUrl = "api/v1/ai/grading/extract-text-from-image";
                
                var payload = new { Base64Image = request.Base64Image, Target = request.Target, SubjectName = request.SubjectName };
                var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");

                var response = await client.PostAsync(apiUrl, content, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorDetails = await response.Content.ReadAsStringAsync(cancellationToken);
                    var sanitized = SanitizeErrorMessage(errorDetails);
                    var httpEx = new HttpRequestException($"EdCo.API returned {(int)response.StatusCode}: {sanitized}");
                    await _errorLogService.LogErrorAsync(httpEx, source: "AdminPortal", logLevel: "Error", customMessage: $"Vision OCR HTTP {(int)response.StatusCode}: {sanitized}");
                    return Json(new { success = false, message = $"API returned {(int)response.StatusCode}: {sanitized}" });
                }

                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                var jsonDoc = System.Text.Json.JsonDocument.Parse(responseBody);
                var root = jsonDoc.RootElement;
                
                var extractedText = root.TryGetProperty("text", out var tp) ? tp.GetString()?.Trim() ?? "" : "";
                var optionA = root.TryGetProperty("optionA", out var oa) ? oa.GetString() ?? "" : "";
                var optionB = root.TryGetProperty("optionB", out var ob) ? ob.GetString() ?? "" : "";
                var optionC = root.TryGetProperty("optionC", out var oc) ? oc.GetString() ?? "" : "";
                var optionD = root.TryGetProperty("optionD", out var od) ? od.GetString() ?? "" : "";
                var correctOption = root.TryGetProperty("correctOption", out var co) ? co.GetString() ?? "" : "";

                return Json(new { success = true, text = extractedText, optionA, optionB, optionC, optionD, correctOption });
            }
            catch (Exception ex)
            {
                await _errorLogService.LogErrorAsync(ex, source: "AdminPortal", logLevel: "Error", customMessage: $"Vision OCR Exception: {ex.Message}");
                
                string userMessage;
                if (ex is TaskCanceledException || ex is TimeoutException || ex.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("MinRequestBodyDataRate", StringComparison.OrdinalIgnoreCase))
                {
                    userMessage = "The upload took too long to complete due to a slow or unstable network connection. Please check your internet connection or try uploading a smaller/cropped image.";
                }
                else
                {
                    userMessage = $"Extraction error: {ex.Message}";
                }

                return Json(new { success = false, message = userMessage });
            }
        }

        // POST: /QuizStudio/GenerateRubric
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateRubric([FromBody] GenerateRubricRequest request, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.QuestionText))
            {
                return Json(new { success = false, message = "Question text is required to generate a rubric." });
            }

            try
            {
                var client = _httpClientFactory.CreateClient("EdCoApi");
                var apiUrl = "api/v1/ai/grading/generate-rubric";

                var payload = new
                {
                    QuestionText = request.QuestionText,
                    ReferenceAnswer = request.ReferenceAnswer ?? "",
                    ReferenceAnswerImageUrl = request.ReferenceAnswerImageUrl,
                    QuestionImageUrl = request.QuestionImageUrl,
                    Points = request.Points
                };

                var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
                var response = await client.PostAsync(apiUrl, content, cancellationToken);

                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var sanitized = SanitizeErrorMessage(responseBody);
                    var httpEx = new HttpRequestException($"EdCo.API returned {(int)response.StatusCode}: {sanitized}");
                    await _errorLogService.LogErrorAsync(httpEx, source: "AdminPortal", logLevel: "Error", customMessage: $"Rubric Generation HTTP {(int)response.StatusCode}: {sanitized}");
                    return Json(new { success = false, message = $"API returned {(int)response.StatusCode}: {sanitized}" });
                }

                var jsonDoc = System.Text.Json.JsonDocument.Parse(responseBody);
                var root = jsonDoc.RootElement;

                var isSuccess = root.TryGetProperty("success", out var s) && s.GetBoolean();
                if (!isSuccess)
                {
                    var msg = root.TryGetProperty("message", out var m) ? m.GetString() : "Generation failed.";
                    return Json(new { success = false, message = msg });
                }

                return Content(responseBody, "application/json");
            }
            catch (Exception ex)
            {
                await _errorLogService.LogErrorAsync(ex, source: "AdminPortal", logLevel: "Error", customMessage: $"Rubric Generation Exception: {ex.Message}");
                return Json(new { success = false, message = $"Exception: {ex.Message}" });
            }
        }

        private static string SanitizeErrorMessage(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "An unknown error occurred.";

            // If response is HTML, strip HTML tags and return clean text summary
            if (raw.TrimStart().StartsWith("<") || raw.Contains("<!DOCTYPE html", StringComparison.OrdinalIgnoreCase) || raw.Contains("<html", StringComparison.OrdinalIgnoreCase))
            {
                var clean = System.Text.RegularExpressions.Regex.Replace(raw, "<.*?>", string.Empty).Trim();
                clean = System.Text.RegularExpressions.Regex.Replace(clean, @"\s+", " ");
                if (clean.Length > 200) clean = clean.Substring(0, 200) + "...";
                return string.IsNullOrWhiteSpace(clean) ? "Server returned an unhandled HTML error response." : clean;
            }

            return raw;
        }
    }
}
