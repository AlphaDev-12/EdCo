using System;

namespace EdCo.Core.Entities
{
    public class AiApiKey : ISoftDelete
    {
        public int Id { get; set; }
        
        // AI Provider: Groq, Gemini, etc.
        public string Provider { get; set; } = "Groq";
        
        // Friendly name: e.g. "Primary Production Key", "Backup Key 1"
        public string Label { get; set; } = string.Empty;
        
        // AES-256 Encrypted key ciphertext
        public string EncryptedApiKey { get; set; } = string.Empty;
        
        // Masked key string for Admin Portal display (e.g., "gsk_aDTr...3Wp")
        public string MaskedKey { get; set; } = string.Empty;
        
        // Indicates if this key is currently active for requests
        public bool IsActive { get; set; } = false;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public DateTime? LastUsedAt { get; set; }
        
        // ISoftDelete implementation
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
    }
}
