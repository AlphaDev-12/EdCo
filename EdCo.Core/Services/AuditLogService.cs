using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EdCo.Core.Data;
using EdCo.Core.Entities;
using EdCo.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Logging;

namespace EdCo.Core.Services
{
    public class AuditLogService : IAuditLogService
    {
        private readonly EdCoDbContext _context;
        private readonly ILogger<AuditLogService> _logger;

        public AuditLogService(EdCoDbContext context, ILogger<AuditLogService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task LogAdminActionAsync(
            string action,
            string entityName,
            string? entityId = null,
            string? details = null,
            string? userId = null,
            string? userName = null,
            string? userRole = null,
            string? ipAddress = null,
            bool isSuccess = true)
        {
            try
            {
                var auditLog = new AuditLog
                {
                    Action = action,
                    EntityName = entityName,
                    EntityId = entityId,
                    Details = details,
                    UserId = userId,
                    UserName = userName,
                    UserRole = userRole,
                    IpAddress = ipAddress,
                    Timestamp = DateTime.UtcNow,
                    IsSuccess = isSuccess
                };

                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception logging admin action for Action: {Action}, Entity: {EntityName}", action, entityName);
            }
        }

        public async Task LogStudentActivityAsync(
            string activityType,
            string? studentId = null,
            string? studentEmail = null,
            string? details = null,
            string? ipAddress = null,
            string? deviceFamily = null)
        {
            try
            {
                var activityLog = new StudentActivityLog
                {
                    ActivityType = activityType,
                    StudentId = studentId,
                    StudentEmail = studentEmail,
                    Details = details,
                    IpAddress = ipAddress,
                    DeviceFamily = deviceFamily,
                    Timestamp = DateTime.UtcNow
                };

                _context.StudentActivityLogs.Add(activityLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception logging student activity for Type: {ActivityType}", activityType);
            }
        }

        public async Task<(List<AuditLog> Items, int TotalCount)> GetAuditLogsAsync(
            int page = 1,
            int pageSize = 20,
            string? search = null,
            string? actionFilter = null,
            string? entityFilter = null,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var query = _context.AuditLogs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(a =>
                    (a.UserName != null && a.UserName.Contains(s)) ||
                    (a.Action != null && a.Action.Contains(s)) ||
                    (a.EntityName != null && a.EntityName.Contains(s)) ||
                    (a.Details != null && a.Details.Contains(s)) ||
                    (a.IpAddress != null && a.IpAddress.Contains(s)));
            }

            if (!string.IsNullOrWhiteSpace(actionFilter))
            {
                query = query.Where(a => a.Action == actionFilter);
            }

            if (!string.IsNullOrWhiteSpace(entityFilter))
            {
                query = query.Where(a => a.EntityName == entityFilter);
            }

            if (startDate.HasValue)
            {
                query = query.Where(a => a.Timestamp >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(a => a.Timestamp <= endDate.Value);
            }

            int totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(a => a.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<(List<StudentActivityLog> Items, int TotalCount)> GetStudentActivitiesAsync(
            int page = 1,
            int pageSize = 20,
            string? search = null,
            string? activityType = null,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var query = _context.StudentActivityLogs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(a =>
                    (a.StudentEmail != null && a.StudentEmail.Contains(s)) ||
                    (a.StudentId != null && a.StudentId.Contains(s)) ||
                    (a.ActivityType != null && a.ActivityType.Contains(s)) ||
                    (a.Details != null && a.Details.Contains(s)));
            }

            if (!string.IsNullOrWhiteSpace(activityType))
            {
                query = query.Where(a => a.ActivityType == activityType);
            }

            if (startDate.HasValue)
            {
                query = query.Where(a => a.Timestamp >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(a => a.Timestamp <= endDate.Value);
            }

            int totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(a => a.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<StudentUsageSummary> GetStudentUsageSummaryAsync()
        {
            var now = DateTime.UtcNow;
            var past24h = now.AddHours(-24);

            int activeStudents = await _context.StudentActivityLogs
                .Where(a => a.Timestamp >= past24h && a.StudentId != null)
                .Select(a => a.StudentId)
                .Distinct()
                .CountAsync();

            int total24h = await _context.StudentActivityLogs
                .CountAsync(a => a.Timestamp >= past24h);

            int quizzesFromLogs = await _context.StudentActivityLogs
                .CountAsync(a => a.ActivityType == "QuizAttempted");

            int quizAttemptsFromDb = await _context.QuizQuestionAttempts.CountAsync();
            int quizzes = Math.Max(quizzesFromLogs, quizAttemptsFromDb);

            int unitsViewed = await _context.StudentActivityLogs
                .CountAsync(a => a.ActivityType == "UnitViewed");

            int aiInteractions = await _context.StudentActivityLogs
                .CountAsync(a => a.ActivityType == "AiTutorEngaged" || a.ActivityType == "AiGradingRequested");

            // 1. Quiz Engagement Telemetry Metrics
            int uniqueQuizStudents = await _context.QuizQuestionAttempts
                .Select(a => a.AppUserId)
                .Distinct()
                .CountAsync();

            if (uniqueQuizStudents == 0)
            {
                uniqueQuizStudents = await _context.StudentActivityLogs
                    .Where(a => a.ActivityType == "QuizAttempted" && a.StudentId != null)
                    .Select(a => a.StudentId)
                    .Distinct()
                    .CountAsync();
            }

            double avgQuizzesPerStudent = uniqueQuizStudents > 0
                ? (double)quizzes / uniqueQuizStudents
                : 0.0;

            // 2. Token & Cost Telemetry Metrics
            long totalTokensConsumed = await _context.AiInteractionLogs
                .SumAsync(l => (long)l.TotalTokens);

            decimal totalAiCost = await _context.AiInteractionLogs
                .SumAsync(l => l.Cost);

            int uniqueAiStudents = await _context.AiInteractionLogs
                .Where(l => l.AppUserId != null)
                .Select(l => l.AppUserId)
                .Distinct()
                .CountAsync();

            int divisorStudents = uniqueAiStudents > 0 ? uniqueAiStudents : (activeStudents > 0 ? activeStudents : 1);

            double avgTokensPerStudent = (double)totalTokensConsumed / divisorStudents;
            decimal avgAiCostPerStudent = totalAiCost / divisorStudents;

            return new StudentUsageSummary
            {
                TotalActiveStudents = activeStudents,
                TotalActivities24h = total24h,
                TotalQuizzesAttempted = quizzes,
                TotalUnitsViewed = unitsViewed,
                TotalAiInteractions = aiInteractions,
                UniqueStudentsWithQuizEngagement = uniqueQuizStudents,
                AverageQuizzesPerStudent = avgQuizzesPerStudent,
                TotalTokensConsumed = totalTokensConsumed,
                AverageTokensPerStudent = avgTokensPerStudent,
                TotalAiCost = totalAiCost,
                AverageAiCostPerStudent = avgAiCostPerStudent
            };
        }
    }
}
