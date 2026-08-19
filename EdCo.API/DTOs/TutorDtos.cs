using System;
using System.ComponentModel.DataAnnotations;

namespace EdCo.API.DTOs
{
    public class CreateSessionDto
    {
        [Required]
        public int SubjectId { get; set; }
        
        [Required]
        public string Topic { get; set; } = string.Empty;
    }

    public class ProcessInteractionDto
    {
        [Required]
        public Guid SessionId { get; set; }
        
        [Required]
        public string UserMessage { get; set; } = string.Empty;
        
        public string? MathExpressionLatex { get; set; }
        
        public string? UploadedImageUrl { get; set; }
    }

    public class ValidateStepDto
    {
        [Required]
        public Guid SessionId { get; set; }
        
        [Required]
        public string CurrentStepLatex { get; set; } = string.Empty;
    }
}
