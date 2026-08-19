using EdCo.API.DTOs;

namespace EdCo.API.Services
{
    public interface IAiRubricService
    {
        Task<(bool Success, int StatusCode, string? ErrorMessage, GenerateRubricResponseDto? Result)> GenerateRubricAsync(GenerateRubricRequestDto request, string? userId);
    }
}
