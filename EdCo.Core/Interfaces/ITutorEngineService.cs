using System;
using System.Threading.Tasks;
using EdCo.Core.Entities;

namespace EdCo.Core.Interfaces
{
    public interface ITutorEngineService
    {
        Task<AiTutorSession> CreateSessionAsync(string userId, int subjectId, string topic);
        Task<IEnumerable<AiTutorSession>> GetSessionsAsync(string userId, int subjectId);
        Task<AiTutorSession?> GetSessionByIdAsync(Guid sessionId, string userId);
        Task<bool> DeleteSessionAsync(Guid sessionId, string userId);
        Task<AiTutorInteraction> ProcessInteractionAsync(Guid sessionId, string userId, string userMessage, string? mathExpressionLatex, string? uploadedImageUrl);
        Task<AiTutorInteraction> ValidateStepAsync(Guid sessionId, string userId, string currentStepLatex);
    }
}
