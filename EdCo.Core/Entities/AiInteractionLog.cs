namespace EdCo.Core.Entities
{
    public class AiInteractionLog
    {
        public int Id { get; set; }
        
        public string? AppUserId { get; set; }
        public AppUser? AppUser { get; set; }
        
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
        
        public string? ModelUsed { get; set; }
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "decimal(18, 8)")]
        public decimal Cost { get; set; }
        
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
