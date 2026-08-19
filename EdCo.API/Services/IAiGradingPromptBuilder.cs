using EdCo.API.DTOs;
using EdCo.Core.Entities;

namespace EdCo.API.Services
{
    public interface IAiGradingPromptBuilder
    {
        string BuildQuestionGradingPrompt(QuizQuestion question, string studentAnswer);
        (string Prompt, List<string> AllImages) BuildImageGradingPrompt(QuizQuestion question, string base64Image, List<string>? base64Images);
        string BuildOcrPrompt(string? subjectName, bool? isQuantitative, bool isAnswerScan);
        (string Prompt, string VisionPrompt, List<string> Images, Dictionary<string, int> BracketMap, int TotalBracketPoints) BuildRubricPrompt(GenerateRubricRequestDto request);
        bool IsQuantitativeSubject(string? subjectName, bool? isQuantitative);
    }
}
