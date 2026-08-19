using System;

namespace EdCo.Core.Entities
{
    public class AiTutorInteraction : ISoftDelete
    {
        public Guid Id { get; set; }
        public Guid SessionId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public AiTutorSession Session { get; set; } = null!;
        
        public string UserMessage { get; set; } = string.Empty;
        public string? MathExpressionLatex { get; set; }
        public string? UploadedImageUrl { get; set; }
        
        public string AiResponse { get; set; } = string.Empty;
        public bool RequiresGraphRender { get; set; }
        
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
    }
}
