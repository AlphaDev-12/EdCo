using System;

namespace EdCo.Core.Entities
{
    public class StudentActivityLog
    {
        public int Id { get; set; }
        public string? StudentId { get; set; }
        public string? StudentEmail { get; set; }
        public string ActivityType { get; set; } = string.Empty;
        public string? Details { get; set; }
        public string? IpAddress { get; set; }
        public string? DeviceFamily { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
