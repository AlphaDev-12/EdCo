using System.Text.Json;
using Microsoft.Extensions.Logging;
using EdCo.API.DTOs;
using EdCo.Core.Exceptions;
using EdCo.Core.Interfaces;

namespace EdCo.API.Services
{
    public class AiRubricService : IAiRubricService
    {
        private readonly IGeminiVisionService _visionService;
        private readonly IErrorLogService _errorLogService;
        private readonly IAiGradingPromptBuilder _promptBuilder;
        private readonly IAiResponseParserService _parserService;
        private readonly ILogger<AiRubricService> _logger;

        public AiRubricService(
            IGeminiVisionService visionService,
            IErrorLogService errorLogService,
            IAiGradingPromptBuilder promptBuilder,
            IAiResponseParserService parserService,
            ILogger<AiRubricService> logger)
        {
            _visionService = visionService;
            _errorLogService = errorLogService;
            _promptBuilder = promptBuilder;
            _parserService = parserService;
            _logger = logger;
        }

        public async Task<(bool Success, int StatusCode, string? ErrorMessage, GenerateRubricResponseDto? Result)> GenerateRubricAsync(GenerateRubricRequestDto request, string? userId)
        {
            if (string.IsNullOrWhiteSpace(request.QuestionText))
            {
                return (false, 400, "Question text is required.", new GenerateRubricResponseDto { Success = false, Message = "Question text is required." });
            }

            bool hasUserPoints = request.Points > 0;
            var (prompt, visionRubricPrompt, images, bracketMap, totalBracketPoints) = _promptBuilder.BuildRubricPrompt(request);

            try
            {
                string aiReply;
                if (images.Count > 0)
                {
                    if (images.Count == 1)
                    {
                        aiReply = await _visionService.ExtractMathFromImageAsync(images[0], visionRubricPrompt, userId);
                    }
                    else
                    {
                        aiReply = await _visionService.ExtractMathFromImagesAsync(images, visionRubricPrompt, userId);
                    }
                }
                else
                {
                    aiReply = await _visionService.GenerateContentAsync(prompt, userId);
                }

                if (string.IsNullOrWhiteSpace(aiReply))
                {
                    return (false, 500, "AI model returned empty response.", new GenerateRubricResponseDto { Success = false, Message = "AI model returned empty response." });
                }

                var criteria = _parserService.ParseRubricCriteria(aiReply, _logger);
                if (criteria == null || criteria.Count == 0)
                {
                    return (false, 500, "Failed to parse generated rubric JSON.", new GenerateRubricResponseDto { Success = false, Message = "Failed to parse generated rubric JSON." });
                }

                foreach (var c in criteria)
                {
                    if (c.MaxPoints < 1) c.MaxPoints = 1;
                }

                if (bracketMap.Count > 0)
                {
                    foreach (var (partLabel, expectedPts) in bracketMap)
                    {
                        var matchingCriteria = criteria.Where(c =>
                            c.Criterion.Contains($"({partLabel})", StringComparison.OrdinalIgnoreCase) ||
                            c.Criterion.Contains($"Part {partLabel}", StringComparison.OrdinalIgnoreCase) ||
                            c.Criterion.Contains($"Part ({partLabel})", StringComparison.OrdinalIgnoreCase) ||
                            c.Criterion.StartsWith(partLabel + " ", StringComparison.OrdinalIgnoreCase) ||
                            c.Criterion.StartsWith(partLabel + " —", StringComparison.OrdinalIgnoreCase) ||
                            c.Criterion.StartsWith(partLabel + "-", StringComparison.OrdinalIgnoreCase)).ToList();

                        if (matchingCriteria.Count > 0)
                        {
                            int currentPartSum = matchingCriteria.Sum(c => c.MaxPoints);
                            if (currentPartSum != expectedPts)
                            {
                                int diff = expectedPts - currentPartSum;
                                var target = matchingCriteria[matchingCriteria.Count - 1];
                                if (target.MaxPoints + diff >= 1)
                                {
                                    target.MaxPoints += diff;
                                }
                            }
                        }
                    }
                }

                if (hasUserPoints && criteria.Count > 0)
                {
                    int totalGeneratedPoints = criteria.Sum(c => c.MaxPoints);
                    if (totalGeneratedPoints != request.Points)
                    {
                        int diff = request.Points - totalGeneratedPoints;
                        var targetCriterion = criteria[criteria.Count - 1];
                        if (targetCriterion.MaxPoints + diff >= 1)
                        {
                            targetCriterion.MaxPoints += diff;
                        }
                        else
                        {
                            int currentSum = criteria.Sum(c => c.MaxPoints);
                            if (criteria.Count > 0) criteria[0].MaxPoints += (request.Points - currentSum);
                        }
                    }
                }

                int totalPoints = criteria.Sum(c => c.MaxPoints);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var finalRubricJson = JsonSerializer.Serialize(criteria, options);

                return (true, 200, null, new GenerateRubricResponseDto
                {
                    Success = true,
                    Criteria = criteria,
                    RubricJson = finalRubricJson,
                    TotalPoints = totalPoints
                });
            }
            catch (GroqRateLimitException grEx)
            {
                _logger.LogWarning(grEx, "Rubric generation rate limited");
                await _errorLogService.LogErrorAsync(grEx, source: "AiTutor", logLevel: "Warning");
                return (false, 429, grEx.Message, new GenerateRubricResponseDto { Success = false, Message = grEx.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Rubric generation failed for prompt request.");
                await _errorLogService.LogErrorAsync(ex, source: "AiTutor", logLevel: "Error");
                return (false, 500, ex.Message, new GenerateRubricResponseDto { Success = false, Message = $"AI error: {ex.Message}" });
            }
        }
    }
}
