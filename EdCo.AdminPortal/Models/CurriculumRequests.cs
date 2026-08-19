using System.Collections.Generic;

namespace EdCo.AdminPortal.Models
{
    public class OrderUpdateRequest
    {
        public List<int>? ChapterOrder { get; set; }
        public List<ChapterUnitGroup>? UnitOrder { get; set; }
    }

    public class ChapterUnitGroup
    {
        public int ChapterId { get; set; }
        public List<int> UnitIds { get; set; } = new();
    }

    public class SaveNotesRequest
    {
        public int UnitId { get; set; }
        public string Markdown { get; set; } = string.Empty;
        public int FlashcardCount { get; set; }
    }

    public class RemoveDocumentRequest
    {
        public int UnitId { get; set; }
    }

    public class AttachVideoRequest
    {
        public int UnitId { get; set; }
        public string BunnyVideoId { get; set; } = string.Empty;
        public int DurationSeconds { get; set; }
    }
}
