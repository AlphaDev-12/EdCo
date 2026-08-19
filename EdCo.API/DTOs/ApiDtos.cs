namespace EdCo.API.DTOs
{
    public class SubjectDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int GradeLevelId { get; set; }
    }

    public class ChapterManifestDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int OrderIndex { get; set; }
        public List<UnitManifestDto> Units { get; set; } = new();
    }

    public class UnitManifestDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int OrderIndex { get; set; }
        public int? VideoId { get; set; }
        public int? NotesId { get; set; }
        public int? QuizId { get; set; }
    }

    public class SyncQuizResultDto
    {
        public int QuizId { get; set; }
        public int Score { get; set; }
        public int TotalQuestions { get; set; }
        public DateTime AttemptedAt { get; set; }
    }

    public class AiTutorRequestDto
    {
        public int? SubjectId { get; set; }
        public int? UnitId { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class AiGradeRequestDto
    {
        public int QuestionId { get; set; }
        public string StudentAnswer { get; set; } = string.Empty;
    }

    public class AiGradeResponseDto
    {
        public int PointsAwarded { get; set; }
        public string CriteriaBreakdown { get; set; } = string.Empty;
        public string Feedback { get; set; } = string.Empty;
    }

    public class AiGradeImageRequestDto
    {
        public int QuestionId { get; set; }
        public string Base64Image { get; set; } = string.Empty;
        public List<string> Base64Images { get; set; } = new();
    }

    public class ExtractTextRequestDto
    {
        public string Base64Image { get; set; } = string.Empty;
        public string Target { get; set; } = "question";
        public string? SubjectName { get; set; }
        public bool? IsQuantitative { get; set; }
    }

    public class UnitDetailsDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ChapterTitle { get; set; } = string.Empty;
        public string? VideoUrl { get; set; }
        public string? NotesUrl { get; set; }
        public string? NotesMarkdown { get; set; }
        public List<QuizQuestionDto> Questions { get; set; } = new();
    }

    public class QuizQuestionDto
    {
        public int Id { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public string? CorrectAnswerImageUrl { get; set; }
        public string QuestionType { get; set; } = string.Empty;
        public int Points { get; set; }
        public string? OptionA { get; set; }
        public string? OptionB { get; set; }
        public string? OptionC { get; set; }
        public string? OptionD { get; set; }
        public string? CorrectOption { get; set; }
    }

    public class GenerateRubricRequestDto
    {
        public string QuestionText { get; set; } = string.Empty;
        public string ReferenceAnswer { get; set; } = string.Empty;
        public string? ReferenceAnswerImageUrl { get; set; }
        public string? QuestionImageUrl { get; set; }
        public int Points { get; set; } = 1;
    }

    public class RubricCriterionDto
    {
        public string Criterion { get; set; } = string.Empty;
        public int MaxPoints { get; set; } = 1;
        public string Description { get; set; } = string.Empty;
    }

    public class GenerateRubricResponseDto
    {
        public bool Success { get; set; } = true;
        public List<RubricCriterionDto> Criteria { get; set; } = new();
        public string RubricJson { get; set; } = "[]";
        public int TotalPoints { get; set; }
        public string? Message { get; set; }
    }

    public class AiBatchGradeRequestDto
    {
        public List<AiGradeRequestDto> Questions { get; set; } = new();
    }

    public class AiBatchGradeResponseDto
    {
        public bool Success { get; set; } = true;
        public List<EdCo.API.Services.AiGradeResponseDtoWithQuestion> GradedQuestions { get; set; } = new();
        public string? Message { get; set; }
    }

    public class QuizSubmissionRequestDto
    {
        public int QuizId { get; set; }
        public List<EdCo.API.Services.GradingQuestionTask> Answers { get; set; } = new();
    }

    public class QuizJobStatusResponseDto
    {
        public string JobId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int TotalQuestions { get; set; }
        public int CompletedQuestions { get; set; }
        public List<EdCo.API.Services.AiGradeResponseDtoWithQuestion> Results { get; set; } = new();
        public string? ErrorMessage { get; set; }
    }

    public class ResetPerformanceDto
    {
        public int? UnitId { get; set; }
        public int? SubjectId { get; set; }
    }
}
