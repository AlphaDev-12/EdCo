using System;

namespace EdCo.Core.Entities
{
    public class AppErrorLog
    {
        public int Id { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Severity Level: Error, Critical, Warning
        public string LogLevel { get; set; } = "Error";
        
        // Component Source: API, AdminPortal, AiTutor, Paynow, Ecocash, BackgroundJob
        public string Source { get; set; } = "API";
        
        public string Message { get; set; } = string.Empty;
        public string? ExceptionType { get; set; }
        public string? StackTrace { get; set; }
        
        // HTTP Context Telemetry
        public string? RequestPath { get; set; }
        public string? HttpMethod { get; set; }
        public int? StatusCode { get; set; }
        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public string? TraceId { get; set; }
        
        // Resolution Workflow
        public bool IsResolved { get; set; } = false;
        public string? ResolvedBy { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string? ResolutionNotes { get; set; }
    }
}
