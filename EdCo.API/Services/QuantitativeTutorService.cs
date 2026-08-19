using System;
using System.Threading.Tasks;
using EdCo.Core.Data;
using EdCo.Core.Entities;
using EdCo.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EdCo.API.Services
{
    public class QuantitativeTutorService : ITutorEngineService
    {
        private readonly EdCoDbContext _dbContext;
        private readonly IGeminiVisionService _geminiVisionService;

        public QuantitativeTutorService(EdCoDbContext dbContext, IGeminiVisionService geminiVisionService)
        {
            _dbContext = dbContext;
            _geminiVisionService = geminiVisionService;
        }

        public async Task<AiTutorSession> CreateSessionAsync(string userId, int subjectId, string topic)
        {
            var session = new AiTutorSession
            {
                Id = Guid.NewGuid(),
                AppUserId = userId,
                SubjectId = subjectId,
                Topic = topic,
                CreatedAt = DateTime.UtcNow,
                LastInteractionAt = DateTime.UtcNow
            };

            _dbContext.AiTutorSessions.Add(session);
            await _dbContext.SaveChangesAsync();

            return session;
        }

        public async Task<IEnumerable<AiTutorSession>> GetSessionsAsync(string userId, int subjectId)
        {
            return await _dbContext.AiTutorSessions
                .Where(s => s.AppUserId == userId && s.SubjectId == subjectId)
                .OrderByDescending(s => s.LastInteractionAt)
                .ToListAsync();
        }

        public async Task<AiTutorSession?> GetSessionByIdAsync(Guid sessionId, string userId)
        {
            return await _dbContext.AiTutorSessions
                .Include(s => s.Interactions)
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.AppUserId == userId);
        }

        public async Task<bool> DeleteSessionAsync(Guid sessionId, string userId)
        {
            var session = await _dbContext.AiTutorSessions
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.AppUserId == userId);

            if (session == null)
            {
                return false;
            }

            _dbContext.AiTutorSessions.Remove(session);
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<AiTutorInteraction> ProcessInteractionAsync(Guid sessionId, string userId, string userMessage, string? mathExpressionLatex, string? uploadedImageUrl)
        {
            var session = await _dbContext.AiTutorSessions
                .Include(s => s.Interactions)
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session == null)
            {
                throw new ArgumentException("Session not found");
            }

            var currentMonth = DateTime.UtcNow.Month;
            var currentYear = DateTime.UtcNow.Year;
            var totalCostThisMonth = await _dbContext.AiInteractionLogs
                .Where(l => l.AppUserId == userId && l.Timestamp.Month == currentMonth && l.Timestamp.Year == currentYear)
                .SumAsync(l => l.Cost);

            if (totalCostThisMonth >= 0.50m)
            {
                throw new InvalidOperationException("QUOTA_EXCEEDED");
            }

            string aiResponse;

            // Build prompt with conversation history
            var promptBuilder = new System.Text.StringBuilder();
            promptBuilder.AppendLine($"You are a Socratic quantitative tutor. Topic: {session.Topic}.");
            promptBuilder.AppendLine("CRITICAL INSTRUCTION: NEVER GIVE THE FINAL ANSWER OR FULL SOLUTION UPFRONT. This is a strict rule. You must act as a Socratic tutor. Guide the user step-by-step using hints and questions.");
            promptBuilder.AppendLine("If the user makes a mistake, gently point out the rule they broke and ask them to try again. ONLY AFTER the user has successfully reached the final answer themselves may you provide a complete summary of the steps.");
            promptBuilder.AppendLine("Formatting rule: Format all mathematical formulas, numbers with subscripts, and expressions using standard inline LaTeX delimiters \\( ... \\) and block LaTeX delimiters \\[ ... \\].");
            promptBuilder.AppendLine("Keep your responses focused. Provide clear step-by-step guidance without writing long conversational paragraphs, but ensure you write out necessary mathematical equations clearly.");
            
            if (session.Interactions != null && session.Interactions.Any())
            {
                promptBuilder.AppendLine("\n--- Previous Conversation ---");
                foreach (var pastInteraction in session.Interactions.OrderBy(i => i.Timestamp))
                {
                    promptBuilder.AppendLine($"User: {pastInteraction.UserMessage}");
                    if (!string.IsNullOrEmpty(pastInteraction.MathExpressionLatex))
                    {
                        promptBuilder.AppendLine($"User Math: {pastInteraction.MathExpressionLatex}");
                    }
                    promptBuilder.AppendLine($"Tutor: {pastInteraction.AiResponse}");
                }
                promptBuilder.AppendLine("---------------------------\n");
            }
            else
            {
                // This is the first interaction in the session. Generate a dynamic topic locally to save API quota.
                if (!string.IsNullOrWhiteSpace(userMessage))
                {
                    var words = userMessage.Split(new[] { ' ', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    var titleWords = words.Take(5).ToArray();
                    var title = string.Join(" ", titleWords);
                    
                    if (title.Length > 0)
                    {
                        title = char.ToUpper(title[0]) + title.Substring(1);
                    }
                    
                    if (words.Length > 5)
                    {
                        title += "...";
                    }
                    
                    session.Topic = title;
                }
                else if (!string.IsNullOrEmpty(uploadedImageUrl))
                {
                    session.Topic = "Image Solution";
                }
            }

            promptBuilder.AppendLine($"Current User message: {userMessage}");

            if (!string.IsNullOrEmpty(mathExpressionLatex))
            {
                promptBuilder.AppendLine($"The user provided this math expression in LaTeX: {mathExpressionLatex}");
            }

            var prompt = promptBuilder.ToString();

            // If we have an image, we use the vision endpoint
            if (!string.IsNullOrEmpty(uploadedImageUrl))
            {
                // In a real app, uploadedImageUrl would be converted to base64 here if it's a URL, 
                // but for this implementation we assume uploadedImageUrl is a base64 string 
                // passed from the frontend for simplicity, or we fetch it.
                // Assuming it's base64 data (e.g. data:image/jpeg;base64,...)
                var base64Data = uploadedImageUrl.Contains(",") 
                    ? uploadedImageUrl.Split(',')[1] 
                    : uploadedImageUrl;
                    
                aiResponse = await _geminiVisionService.ExtractMathFromImageAsync(base64Data, prompt, userId);
            }
            else
            {
                aiResponse = await _geminiVisionService.GenerateContentAsync(prompt, userId);
            }

            var interaction = new AiTutorInteraction
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                UserMessage = userMessage,
                MathExpressionLatex = mathExpressionLatex,
                UploadedImageUrl = uploadedImageUrl,
                AiResponse = aiResponse,
                Timestamp = DateTime.UtcNow,
                RequiresGraphRender = aiResponse.Contains("```tikz") // Basic heuristic
            };

            session.LastInteractionAt = DateTime.UtcNow;
            _dbContext.AiTutorInteractions.Add(interaction);
            await _dbContext.SaveChangesAsync();

            return interaction;
        }

        public async Task<AiTutorInteraction> ValidateStepAsync(Guid sessionId, string userId, string currentStepLatex)
        {
            var session = await _dbContext.AiTutorSessions
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session == null)
            {
                throw new ArgumentException("Session not found");
            }

            var currentMonth = DateTime.UtcNow.Month;
            var currentYear = DateTime.UtcNow.Year;
            var totalCostThisMonth = await _dbContext.AiInteractionLogs
                .Where(l => l.AppUserId == userId && l.Timestamp.Month == currentMonth && l.Timestamp.Year == currentYear)
                .SumAsync(l => l.Cost);

            if (totalCostThisMonth >= 0.50m)
            {
                throw new InvalidOperationException("QUOTA_EXCEEDED");
            }

            var prompt = $"You are a Socratic tutor. The student submitted this step: {currentStepLatex}. " +
                         "Is this mathematical step logically valid? If not, why? " +
                         "Do not solve the rest of the problem.";

            var aiResponse = await _geminiVisionService.GenerateContentAsync(prompt, userId);

            var interaction = new AiTutorInteraction
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                UserMessage = "Validating step...",
                MathExpressionLatex = currentStepLatex,
                AiResponse = aiResponse,
                Timestamp = DateTime.UtcNow
            };

            session.LastInteractionAt = DateTime.UtcNow;
            _dbContext.AiTutorInteractions.Add(interaction);
            await _dbContext.SaveChangesAsync();

            return interaction;
        }
    }
}
