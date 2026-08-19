using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EdCo.Core.Entities;

namespace EdCo.Core.Interfaces
{
    public interface IAuditLogService
    {
        Task LogAdminActionAsync(
            string action,
            string entityName,
            string? entityId = null,
            string? details = null,
            string? userId = null,
            string? userName = null,
            string? userRole = null,
            string? ipAddress = null,
            bool isSuccess = true);

        Task LogStudentActivityAsync(
            string activityType,
            string? studentId = null,
            string? studentEmail = null,
            string? details = null,
            string? ipAddress = null,
            string? deviceFamily = null);

        Task<(List<AuditLog> Items, int TotalCount)> GetAuditLogsAsync(
            int page = 1,
            int pageSize = 20,
            string? search = null,
            string? actionFilter = null,
            string? entityFilter = null,
            DateTime? startDate = null,
            DateTime? endDate = null);

        Task<(List<StudentActivityLog> Items, int TotalCount)> GetStudentActivitiesAsync(
            int page = 1,
            int pageSize = 20,
            string? search = null,
            string? activityType = null,
            DateTime? startDate = null,
            DateTime? endDate = null);

        Task<StudentUsageSummary> GetStudentUsageSummaryAsync();
    }

    public class StudentUsageSummary
    {
        public int TotalActiveStudents { get; set; }
        public int TotalActivities24h { get; set; }
        public int TotalQuizzesAttempted { get; set; }
        public int TotalUnitsViewed { get; set; }
        public int TotalAiInteractions { get; set; }

        // Extended Telemetry Metrics
        public int UniqueStudentsWithQuizEngagement { get; set; }
        public double AverageQuizzesPerStudent { get; set; }
        public long TotalTokensConsumed { get; set; }
        public double AverageTokensPerStudent { get; set; }
        public decimal TotalAiCost { get; set; }
        public decimal AverageAiCostPerStudent { get; set; }
    }
}
