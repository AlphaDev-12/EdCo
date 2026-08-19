using Microsoft.Extensions.Logging;
using EdCo.API.DTOs;
using EdCo.Core.Exceptions;
using EdCo.Core.Interfaces;

namespace EdCo.API.Services
{
    public class OcrExtractionService : IOcrExtractionService
    {
        private readonly IGeminiVisionService _visionService;
        private readonly IErrorLogService _errorLogService;
        private readonly IAiGradingPromptBuilder _promptBuilder;
        private readonly IAiResponseParserService _parserService;
        private readonly ILogger<OcrExtractionService> _logger;

        public OcrExtractionService(
            IGeminiVisionService visionService,
            IErrorLogService errorLogService,
            IAiGradingPromptBuilder promptBuilder,
            IAiResponseParserService parserService,
            ILogger<OcrExtractionService> logger)
        {
            _visionService = visionService;
            _errorLogService = errorLogService;
            _promptBuilder = promptBuilder;
            _parserService = parserService;
            _logger = logger;
        }

        public async Task<(bool Success, int StatusCode, string? ErrorMessage, ExtractedOcrResultDto? Result)> ExtractTextFromImageAsync(ExtractTextRequestDto request, string? userId)
        {
            if (string.IsNullOrWhiteSpace(request.Base64Image))
            {
                return (false, 400, "No image provided.", null);
            }

            bool isAnswerScan = string.Equals(request.Target, "answer", StringComparison.OrdinalIgnoreCase);
            var prompt = _promptBuilder.BuildOcrPrompt(request.SubjectName, request.IsQuantitative, isAnswerScan);

            var base64Data = request.Base64Image.Contains(",")
                ? request.Base64Image.Split(',')[1]
                : request.Base64Image;

            try
            {
                var aiReply = await _visionService.ExtractMathFromImageAsync(base64Data, prompt, userId);
                var ocrResult = _parserService.ParseOcrResponse(aiReply, _logger);

                return (true, 200, null, ocrResult);
            }
            catch (GroqRateLimitException grEx)
            {
                _logger.LogWarning(grEx, "Vision AI extraction rate limited");
                await _errorLogService.LogErrorAsync(grEx, source: "AiTutor", logLevel: "Warning");
                return (false, 429, grEx.Message, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Vision AI extraction failed");
                await _errorLogService.LogErrorAsync(ex, source: "AiTutor", logLevel: "Error");
                return (false, 500, "Failed to reach vision AI provider.", null);
            }
        }
    }
}
