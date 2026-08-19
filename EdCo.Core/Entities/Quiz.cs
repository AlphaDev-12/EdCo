using System;

namespace EdCo.Core.Entities
{
    public class Quiz : ISoftDelete
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        
        // A Quiz can belong to a Unit OR directly to a Subject (as an Exam)
        public int? UnitId { get; set; }
        public Unit? Unit { get; set; }
        
        public int? SubjectId { get; set; }
        public Subject? Subject { get; set; }

        public bool IsExam { get; set; } = false;
        public int DisplayPosition { get; set; } = 0;
        
        public ICollection<QuizQuestion> Questions { get; set; } = new List<QuizQuestion>();

        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
    }
}

