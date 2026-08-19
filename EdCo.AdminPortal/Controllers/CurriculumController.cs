using EdCo.Core.Data;
using EdCo.Core.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using EdCo.AdminPortal.Models;

namespace EdCo.AdminPortal.Controllers
{
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class CurriculumController : Controller
    {
        private readonly EdCoDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly EdCo.Core.Interfaces.IFileSecurityService _fileSecurityService;
        private readonly EdCo.Core.Interfaces.ILocalFileStorageService _storageService;

        private readonly EdCo.Core.Interfaces.ICacheService _cacheService;
        private readonly EdCo.Core.Interfaces.IAuditLogService _auditLogService;
        private readonly ILogger<CurriculumController> _logger;

        public CurriculumController(
            EdCoDbContext context, 
            IConfiguration configuration, 
            HttpClient httpClient,
            EdCo.Core.Interfaces.IFileSecurityService fileSecurityService,
            EdCo.Core.Interfaces.ILocalFileStorageService storageService,
            EdCo.Core.Interfaces.ICacheService cacheService,
            EdCo.Core.Interfaces.IAuditLogService auditLogService,
            ILogger<CurriculumController> logger)
        {
            _context = context;
            _configuration = configuration;
            _httpClient = httpClient;
            _fileSecurityService = fileSecurityService;
            _storageService = storageService;
            _cacheService = cacheService;
            _auditLogService = auditLogService;
            _logger = logger;
        }

        private string GetCurrentUserId() => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
        private string GetCurrentUserName() => User.Identity?.Name ?? "Admin";
        private string GetCurrentUserRole() => User.IsInRole("SuperAdmin") ? "SuperAdmin" : "Admin";
        private string GetClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

        // GET: /Curriculum — Subject picker
        public async Task<IActionResult> Index()
        {
            var subjects = await _context.Subjects
                .Include(s => s.GradeLevel)
                .Include(s => s.Chapters)
                    .ThenInclude(c => c.Units)
                .OrderBy(s => s.GradeLevel.Name)
                .ThenBy(s => s.Name)
                .ToListAsync();
            return View(subjects);
        }

        // GET: /Curriculum/Builder/5 — The drag-and-drop canvas
        public async Task<IActionResult> Builder(int id)
        {
            var subject = await _context.Subjects
                .Include(s => s.GradeLevel)
                .Include(s => s.Chapters.OrderBy(c => c.OrderIndex))
                    .ThenInclude(c => c.Units.OrderBy(u => u.OrderIndex))
                        .ThenInclude(u => u.Video)
                .Include(s => s.Chapters)
                    .ThenInclude(c => c.Units)
                        .ThenInclude(u => u.Notes)
                .Include(s => s.Chapters)
                    .ThenInclude(c => c.Units)
                        .ThenInclude(u => u.Quiz)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (subject == null) return NotFound();
            return View(subject);
        }

        // POST: /Curriculum/AddChapter
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddChapter(int subjectId, string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                TempData["Error"] = "Chapter title is required.";
                return RedirectToAction(nameof(Builder), new { id = subjectId });
            }

            var maxOrder = await _context.Chapters
                .Where(c => c.SubjectId == subjectId)
                .MaxAsync(c => (int?)c.OrderIndex) ?? 0;

            var newChapter = new Chapter
            {
                Title = title,
                SubjectId = subjectId,
                OrderIndex = maxOrder + 1
            };
            _context.Chapters.Add(newChapter);
            await _context.SaveChangesAsync();
            await _cacheService.RemoveAsync($"Curriculum:Manifest:{subjectId}");

            await _auditLogService.LogAdminActionAsync(
                action: "AddChapter",
                entityName: "Chapter",
                entityId: newChapter.Id.ToString(),
                details: $"Created chapter '{title}' in subject #{subjectId}",
                userId: GetCurrentUserId(),
                userName: GetCurrentUserName(),
                userRole: GetCurrentUserRole(),
                ipAddress: GetClientIp());

            TempData["Success"] = $"Chapter '{title}' added.";
            return RedirectToAction(nameof(Builder), new { id = subjectId });
        }

        // POST: /Curriculum/AddUnit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddUnit(int chapterId, string title, int subjectId)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                TempData["Error"] = "Unit title is required.";
                return RedirectToAction(nameof(Builder), new { id = subjectId });
            }

            var maxOrder = await _context.Units
                .Where(u => u.ChapterId == chapterId)
                .MaxAsync(u => (int?)u.OrderIndex) ?? 0;

            var newUnit = new Unit
            {
                Title = title,
                ChapterId = chapterId,
                OrderIndex = maxOrder + 1
            };
            _context.Units.Add(newUnit);
            await _context.SaveChangesAsync();
            await _cacheService.RemoveAsync($"Curriculum:Manifest:{subjectId}");

            await _auditLogService.LogAdminActionAsync(
                action: "AddUnit",
                entityName: "Unit",
                entityId: newUnit.Id.ToString(),
                details: $"Created unit '{title}' under chapter #{chapterId}",
                userId: GetCurrentUserId(),
                userName: GetCurrentUserName(),
                userRole: GetCurrentUserRole(),
                ipAddress: GetClientIp());

            TempData["Success"] = $"Unit '{title}' added.";
            return RedirectToAction(nameof(Builder), new { id = subjectId });
        }

        // POST: /Curriculum/UpdateOrder — AJAX endpoint for drag-and-drop
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateOrder([FromBody] OrderUpdateRequest request)
        {
            if (request.ChapterOrder != null)
            {
                for (int i = 0; i < request.ChapterOrder.Count; i++)
                {
                    var chapter = await _context.Chapters.FindAsync(request.ChapterOrder[i]);
                    if (chapter != null)
                    {
                        chapter.OrderIndex = i + 1;
                    }
                }
            }

            if (request.UnitOrder != null)
            {
                foreach (var chapterGroup in request.UnitOrder)
                {
                    for (int i = 0; i < chapterGroup.UnitIds.Count; i++)
                    {
                        var unit = await _context.Units.FindAsync(chapterGroup.UnitIds[i]);
                        if (unit != null)
                        {
                            unit.ChapterId = chapterGroup.ChapterId;
                            unit.OrderIndex = i + 1;
                        }
                    }
                }
            }

            await _context.SaveChangesAsync();
            await _cacheService.RemoveByPrefixAsync("Curriculum:Manifest:");
            return Json(new { success = true });
        }

        // POST: /Curriculum/SaveNotes — AJAX endpoint for markdown notes
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveNotes([FromBody] SaveNotesRequest request)
        {
            var notes = await _context.NotesContents.FirstOrDefaultAsync(n => n.UnitId == request.UnitId);
            if (notes == null)
            {
                notes = new NotesContent
                {
                    UnitId = request.UnitId,
                    MarkdownBlob = request.Markdown
                };
                _context.NotesContents.Add(notes);
            }
            else
            {
                notes.MarkdownBlob = request.Markdown;
            }

            await _context.SaveChangesAsync();

            if (request.FlashcardCount > 0)
            {
                await GenerateAndSaveFlashcardsAsync(request.UnitId, request.Markdown, request.FlashcardCount);
            }

            return Json(new { success = true });
        }

        // GET: /Curriculum/GetNotes?unitId=5
        [HttpGet]
        public async Task<IActionResult> GetNotes(int unitId)
        {
            var notes = await _context.NotesContents.FirstOrDefaultAsync(n => n.UnitId == unitId);
            return Json(new { 
                markdown = notes?.MarkdownBlob ?? "",
                documentUrl = notes?.DocumentUrl ?? "",
                documentFileName = notes?.DocumentFileName ?? ""
            });
        }

        // POST: /Curriculum/UploadNotesDocument
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(15 * 1024 * 1024)] // 15MB limit
        public async Task<IActionResult> UploadNotesDocument(int unitId, int flashcardCount, IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return Json(new { success = false, message = "No file uploaded." });
            }

            // 1. Strict Security Validation & Scan (MIME, Magic Bytes, Extension Whitelist, Size, Windows Defender)
            var allowedExtensions = new[] { ".pdf", ".txt", ".md", ".docx", ".csv" };
            var (isValid, errorMessage) = await _fileSecurityService.ValidateAndScanAsync(file, allowedExtensions, maxByteSize: 15 * 1024 * 1024);

            if (!isValid)
            {
                return Json(new { success = false, message = errorMessage });
            }

            // 2. Offload to Secure Local Storage outside wwwroot
            var uniqueFileName = await _storageService.SaveFileAsync(file, "documents");
            var fileUrl = $"/Curriculum/Document?fileName={uniqueFileName}";
            string extractedText = string.Empty;

            // 3. Extract Text safely from secure file stream
            try
            {
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                var (stream, _) = _storageService.GetFileStream(uniqueFileName, "documents");
                using (stream)
                {
                    if (ext == ".txt" || ext == ".md" || ext == ".csv")
                    {
                        using var reader = new StreamReader(stream, Encoding.UTF8);
                        extractedText = await reader.ReadToEndAsync();
                    }
                    else if (ext == ".pdf")
                    {
                        using var pdfDocument = PdfDocument.Open(stream);
                        var textBuilder = new StringBuilder();
                        foreach (var page in pdfDocument.GetPages())
                        {
                            textBuilder.AppendLine(page.Text);
                        }
                        extractedText = textBuilder.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract text from document for Unit {UnitId}", unitId);
            }

            // 4. Save to Database
            var notes = await _context.NotesContents.FirstOrDefaultAsync(n => n.UnitId == unitId);
            if (notes == null)
            {
                notes = new NotesContent
                {
                    UnitId = unitId,
                    MarkdownBlob = "",
                    DocumentUrl = fileUrl,
                    DocumentFileName = file.FileName,
                    ExtractedDocumentText = extractedText
                };
                _context.NotesContents.Add(notes);
            }
            else
            {
                notes.DocumentUrl = fileUrl;
                notes.DocumentFileName = file.FileName;
                notes.ExtractedDocumentText = extractedText;
            }

            await _context.SaveChangesAsync();

            if (flashcardCount > 0 && !string.IsNullOrWhiteSpace(extractedText))
            {
                await GenerateAndSaveFlashcardsAsync(unitId, extractedText, flashcardCount);
            }

            return Json(new { success = true, fileUrl = fileUrl, fileName = file.FileName });
        }

        // GET: /Curriculum/Document?fileName=xxx
        [HttpGet]
        public IActionResult Document(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return NotFound();

            try
            {
                var (stream, contentType) = _storageService.GetFileStream(fileName, "documents");
                return File(stream, contentType, fileName);
            }
            catch (FileNotFoundException)
            {
                return NotFound("Document not found.");
            }
            catch (UnauthorizedAccessException)
            {
                return BadRequest("Invalid filename specified.");
            }
            catch (Exception)
            {
                return StatusCode(500, "Error retrieving document.");
            }
        }

        // POST: /Curriculum/RemoveNotesDocument
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveNotesDocument([FromBody] RemoveDocumentRequest request)
        {
            var notes = await _context.NotesContents.FirstOrDefaultAsync(n => n.UnitId == request.UnitId);
            if (notes != null)
            {
                notes.DocumentUrl = null;
                notes.DocumentFileName = null;
                await _context.SaveChangesAsync();
            }
            return Json(new { success = true });
        }

        // POST: /Curriculum/UploadVideo
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(1_000_000_000)] // Allow up to 1GB
        [RequestFormLimits(MultipartBodyLengthLimit = 1_000_000_000)]
        public async Task<IActionResult> UploadVideo(int unitId, IFormFile videoFile)
        {
            if (videoFile == null || videoFile.Length == 0)
                return Json(new { success = false, message = "No video file provided." });

            var bunnyConfig = _configuration.GetSection("BunnyNet");
            var apiKey = bunnyConfig["ApiKey"];
            var libraryId = bunnyConfig["LibraryId"];

            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(libraryId))
                return Json(new { success = false, message = "Bunny.net API keys are not configured." });

            try
            {
                using var client = new HttpClient();
                client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
                client.DefaultRequestHeaders.Add("AccessKey", apiKey);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // Step 1: Create Video Object
                var createPayload = new { title = $"Unit_{unitId}_{Path.GetFileNameWithoutExtension(videoFile.FileName)}" };
                var createContent = new StringContent(JsonSerializer.Serialize(createPayload), Encoding.UTF8, "application/json");
                
                var createResponse = await client.PostAsync($"https://video.bunnycdn.com/library/{libraryId}/videos", createContent);
                if (!createResponse.IsSuccessStatusCode)
                {
                    var err = await createResponse.Content.ReadAsStringAsync();
                    return Json(new { success = false, message = $"Failed to create video object in Bunny.net. HTTP {createResponse.StatusCode}: {err}" });
                }

                var createResponseBody = await createResponse.Content.ReadAsStringAsync();
                using var jsonDoc = JsonDocument.Parse(createResponseBody);
                var bunnyVideoId = jsonDoc.RootElement.GetProperty("guid").GetString();

                if (string.IsNullOrEmpty(bunnyVideoId))
                    return Json(new { success = false, message = "Bunny.net did not return a valid video GUID." });

                // Step 2: Upload Video File using a local temp file to avoid stream deadlocks
                var tempFilePath = Path.GetTempFileName();
                try
                {
                    using (var fs = new FileStream(tempFilePath, FileMode.Create))
                    {
                        await videoFile.CopyToAsync(fs);
                    }

                    using var fileStream = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read);
                    using var streamContent = new StreamContent(fileStream);
                    streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    // Explicitly set Content-Length from FileStream
                    streamContent.Headers.ContentLength = fileStream.Length;

                    client.DefaultRequestHeaders.ExpectContinue = false;
                    
                    var requestMessage = new HttpRequestMessage(HttpMethod.Put, $"https://video.bunnycdn.com/library/{libraryId}/videos/{bunnyVideoId}")
                    {
                        Content = streamContent
                    };

                    var uploadResponse = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);
                    if (!uploadResponse.IsSuccessStatusCode)
                    {
                        var err = await uploadResponse.Content.ReadAsStringAsync();
                        return Json(new { success = false, message = $"Failed to upload video to Bunny.net. HTTP {uploadResponse.StatusCode}: {err}" });
                    }
                }
                finally
                {
                    if (System.IO.File.Exists(tempFilePath))
                        System.IO.File.Delete(tempFilePath);
                }

                // Step 3: Save to Database
                var video = await _context.VideoAssets.FirstOrDefaultAsync(v => v.UnitId == unitId);
                if (video == null)
                {
                    video = new VideoAsset
                    {
                        UnitId = unitId,
                        BunnyVideoId = bunnyVideoId,
                        EncryptedStreamUrl = $"https://iframe.mediadelivery.net/embed/{libraryId}/{bunnyVideoId}",
                        DurationSeconds = 0 // Optional: fetch actual duration via API later
                    };
                    _context.VideoAssets.Add(video);
                }
                else
                {
                    video.BunnyVideoId = bunnyVideoId;
                    video.EncryptedStreamUrl = $"https://iframe.mediadelivery.net/embed/{libraryId}/{bunnyVideoId}";
                }

                await _context.SaveChangesAsync();

                await _auditLogService.LogAdminActionAsync(
                    action: "UploadVideo",
                    entityName: "VideoAsset",
                    entityId: video.Id.ToString(),
                    details: $"Uploaded video for unit #{unitId} (Bunny ID: {bunnyVideoId})",
                    userId: GetCurrentUserId(),
                    userName: GetCurrentUserName(),
                    userRole: GetCurrentUserRole(),
                    ipAddress: GetClientIp());

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                var innerMsg = ex.InnerException != null ? ex.InnerException.Message : "";
                return Json(new { success = false, message = $"Exception: {ex.Message} {innerMsg}" });
            }
        }

        // POST: /Curriculum/DeleteChapter
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteChapter(int id, int subjectId)
        {
            var chapter = await _context.Chapters.FindAsync(id);
            if (chapter != null)
            {
                chapter.DeletedBy = GetCurrentUserName();
                _context.Chapters.Remove(chapter);
                await _context.SaveChangesAsync();
                await _cacheService.RemoveAsync($"Curriculum:Manifest:{subjectId}");

                await _auditLogService.LogAdminActionAsync(
                    action: "DeleteChapter",
                    entityName: "Chapter",
                    entityId: id.ToString(),
                    details: $"Soft deleted chapter '{chapter.Title}' (Id: {id}) from subject #{subjectId}",
                    userId: GetCurrentUserId(),
                    userName: GetCurrentUserName(),
                    userRole: GetCurrentUserRole(),
                    ipAddress: GetClientIp());

                TempData["Success"] = $"Chapter '{chapter.Title}' deleted (soft-delete).";
            }
            return RedirectToAction(nameof(Builder), new { id = subjectId });
        }

        // POST: /Curriculum/DeleteUnit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUnit(int id, int subjectId)
        {
            var unit = await _context.Units.FindAsync(id);
            if (unit != null)
            {
                unit.DeletedBy = GetCurrentUserName();
                _context.Units.Remove(unit);
                await _context.SaveChangesAsync();
                await _cacheService.RemoveAsync($"Curriculum:Manifest:{subjectId}");

                await _auditLogService.LogAdminActionAsync(
                    action: "DeleteUnit",
                    entityName: "Unit",
                    entityId: id.ToString(),
                    details: $"Soft deleted unit '{unit.Title}' (Id: {id}) from subject #{subjectId}",
                    userId: GetCurrentUserId(),
                    userName: GetCurrentUserName(),
                    userRole: GetCurrentUserRole(),
                    ipAddress: GetClientIp());

                TempData["Success"] = $"Unit '{unit.Title}' deleted (soft-delete).";
            }
            return RedirectToAction(nameof(Builder), new { id = subjectId });
        }

        private async Task GenerateAndSaveFlashcardsAsync(int unitId, string textContext, int count)
        {
            if (string.IsNullOrWhiteSpace(textContext)) return;

            // Simple truncation to fit context window if needed
            if (textContext.Length > 20000) textContext = textContext.Substring(0, 20000);

            var groqConfig = _configuration.GetSection("Groq");
            var apiKey = groqConfig["ApiKey"] ?? "";
            var baseUrl = groqConfig["BaseUrl"] ?? "https://api.groq.com/openai/v1";
            var modelName = groqConfig["Model"] ?? "llama-3.1-8b-instant";

            var systemPrompt = $"You are an expert educator. Extract exactly {count} key facts from the provided text and format them as flashcards. " +
                               "You MUST respond ONLY with a valid JSON array of objects. Do not include any markdown formatting, backticks, or other text outside the JSON array. " +
                               "Format: [{\"front\": \"Question or Concept\", \"back\": \"Answer or Definition\"}]";

            var payload = new
            {
                model = modelName,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = textContext }
                },
                max_tokens = 2000,
                temperature = 0.3
            };

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            requestMessage.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.SendAsync(requestMessage);
                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    var jsonDoc = JsonDocument.Parse(responseBody);
                    var replyMessage = jsonDoc.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString();

                    if (!string.IsNullOrWhiteSpace(replyMessage))
                    {
                        // Clean up markdown block if the model included it despite instructions
                        var jsonString = replyMessage.Trim();
                        if (jsonString.StartsWith("```json"))
                        {
                            jsonString = jsonString.Substring(7);
                            if (jsonString.EndsWith("```")) jsonString = jsonString.Substring(0, jsonString.Length - 3);
                        }
                        
                        var flashcardsData = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(jsonString);
                        
                        if (flashcardsData != null && flashcardsData.Count > 0)
                        {
                            // Remove old flashcards
                            var oldCards = await _context.Flashcards.Where(f => f.UnitId == unitId).ToListAsync();
                            _context.Flashcards.RemoveRange(oldCards);

                            // Insert new
                            foreach (var cardData in flashcardsData)
                            {
                                if (cardData.TryGetValue("front", out var front) && cardData.TryGetValue("back", out var back))
                                {
                                    _context.Flashcards.Add(new Flashcard
                                    {
                                        UnitId = unitId,
                                        FrontContent = front,
                                        BackContent = back
                                    });
                                }
                            }
                            await _context.SaveChangesAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate flashcards for Unit {UnitId}", unitId);
            }
        }
    }
}
