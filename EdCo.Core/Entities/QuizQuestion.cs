using System;

namespace EdCo.Core.Entities
{
    public class QuizQuestion : ISoftDelete
    {
        public int Id { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        
        public QuestionType QuestionType { get; set; } = QuestionType.MultipleChoice;
        public int Points { get; set; } = 1;
        
        // For Multiple Choice
        public string? OptionA { get; set; }
        public string? OptionB { get; set; }
        public string? OptionC { get; set; }
        public string? OptionD { get; set; }
        public string? CorrectOption { get; set; } // A, B, C, or D
        
        // For Short Answer / Essay
        public string? CorrectAnswer { get; set; }
        public string? CorrectAnswerImageUrl { get; set; }
        public string? RubricJson { get; set; }
        
        public int QuizId { get; set; }
        public Quiz Quiz { get; set; } = null!;

        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
    }
}

