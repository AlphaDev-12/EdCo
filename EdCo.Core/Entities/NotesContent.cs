using System;

namespace EdCo.Core.Entities
{
    public class NotesContent : ISoftDelete
    {
        public int Id { get; set; }
        public string MarkdownBlob { get; set; } = string.Empty;
        public string? DocumentUrl { get; set; }
        public string? DocumentFileName { get; set; }
        public string? ExtractedDocumentText { get; set; }
        
        public int UnitId { get; set; }
        public Unit Unit { get; set; } = null!;

        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
    }
}

