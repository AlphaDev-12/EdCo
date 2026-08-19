using EdCo.API.DTOs;

namespace EdCo.API.Services
{
    public interface IOcrExtractionService
    {
        Task<(bool Success, int StatusCode, string? ErrorMessage, ExtractedOcrResultDto? Result)> ExtractTextFromImageAsync(ExtractTextRequestDto request, string? userId);
    }
}
