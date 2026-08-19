using EdCo.Core.Entities;

namespace EdCo.AdminPortal.Models
{
    public class CreateQuizRequest
    {
        public int UnitId { get; set; }
        public string Title { get; set; } = string.Empty;
    }

    public class AddQuestionRequest
    {
        public int QuizId { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public QuestionType QuestionType { get; set; } = QuestionType.MultipleChoice;
        public int Points { get; set; } = 1;
        public string? OptionA { get; set; }
        public string? OptionB { get; set; }
        public string? OptionC { get; set; }
        public string? OptionD { get; set; }
        public string? CorrectOption { get; set; }
        public string? CorrectAnswer { get; set; }
        public string? CorrectAnswerImageUrl { get; set; }
        public string? RubricJson { get; set; }
        public string? ImageUrl { get; set; }
    }

    public class UpdateQuestionRequest
    {
        public int Id { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public QuestionType QuestionType { get; set; } = QuestionType.MultipleChoice;
        public int Points { get; set; } = 1;
        public string? OptionA { get; set; }
        public string? OptionB { get; set; }
        public string? OptionC { get; set; }
        public string? OptionD { get; set; }
        public string? CorrectOption { get; set; }
        public string? CorrectAnswer { get; set; }
        public string? CorrectAnswerImageUrl { get; set; }
        public string? RubricJson { get; set; }
        public string? ImageUrl { get; set; }
    }

    public class UpdateTitleRequest
    {
        public int QuizId { get; set; }
        public string Title { get; set; } = string.Empty;
    }

    public class CreateExamRequest
    {
        public int SubjectId { get; set; }
        public string Title { get; set; } = string.Empty;
    }

    public class UpdateExamStatusRequest
    {
        public int QuizId { get; set; }
        public int DisplayPosition { get; set; }
    }

    public class ExtractTextRequest
    {
        public string Base64Image { get; set; } = string.Empty;
        public string Target { get; set; } = "question";
        public string? SubjectName { get; set; }
    }

    public class GenerateRubricRequest
    {
        public string QuestionText { get; set; } = string.Empty;
        public string? ReferenceAnswer { get; set; }
        public string? ReferenceAnswerImageUrl { get; set; }
        public string? QuestionImageUrl { get; set; }
        public int Points { get; set; } = 1;
    }
}
