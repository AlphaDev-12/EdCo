using Microsoft.Extensions.Logging;
using EdCo.API.DTOs;

namespace EdCo.API.Services
{
    public interface IAiResponseParserService
    {
        AiGradeResponseDto? CleanAndParseGradeResponse(string raw);
        string CleanJsonResponse(string raw);
        string SanitizeJsonMath(string jsonText);
        List<RubricCriterionDto>? ParseRubricCriteria(string raw, ILogger logger);
        ExtractedOcrResultDto ParseOcrResponse(string cleanedReply, ILogger logger);
    }

    public class ExtractedOcrResultDto
    {
        public string Text { get; set; } = string.Empty;
        public string OptionA { get; set; } = string.Empty;
        public string OptionB { get; set; } = string.Empty;
        public string OptionC { get; set; } = string.Empty;
        public string OptionD { get; set; } = string.Empty;
        public string CorrectOption { get; set; } = string.Empty;
    }
}
