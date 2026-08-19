using System;

namespace EdCo.Core.Entities
{
    public class VideoAsset : ISoftDelete
    {
        public int Id { get; set; }
        public string BunnyVideoId { get; set; } = string.Empty;
        public string EncryptedStreamUrl { get; set; } = string.Empty;
        public int DurationSeconds { get; set; }
        
        public int UnitId { get; set; }
        public Unit Unit { get; set; } = null!;

        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
    }
}

