using Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using EdCo.API.Services;
using EdCo.API.DTOs;
using EdCo.Core.Entities;

namespace EdCo.Tests
{
    public class AiGradingSolidServicesTests
    {
        [Theory]
        [InlineData("Mathematics", null, true)]
        [InlineData("Physics", null, true)]
        [InlineData("English Language", null, false)]
        [InlineData("History", null, false)]
        [InlineData("Custom Subject", true, true)]
        [InlineData("Custom Subject", false, false)]
        public void IsQuantitativeSubject_ClassifiesCorrectly(string subjectName, bool? isQuant, bool expected)
        {
            var promptBuilder = new AiGradingPromptBuilder();
            var result = promptBuilder.IsQuantitativeSubject(subjectName, isQuant);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void BuildQuestionGradingPrompt_IncludesQuestionAndAnswerText()
        {
            var promptBuilder = new AiGradingPromptBuilder();
            var question = new QuizQuestion
            {
                Id = 1,
                QuestionText = "Calculate 2 + 2",
                Points = 2,
                CorrectAnswer = "4",
                RubricJson = "[]"
            };

            var prompt = promptBuilder.BuildQuestionGradingPrompt(question, "4");

            Assert.Contains("Calculate 2 + 2", prompt);
            Assert.Contains("Student Answer:\n4", prompt);
            Assert.Contains("Max Points: 2", prompt);
        }

        [Fact]
        public void BuildImageGradingPrompt_HandlesMultipleImagesAndGuides()
        {
            var promptBuilder = new AiGradingPromptBuilder();
            var question = new QuizQuestion
            {
                Id = 2,
                QuestionText = "Graph the line y = 2x + 1",
                ImageUrl = "data:image/png;base64,QSU...",
                CorrectAnswerImageUrl = "data:image/png;base64,REF..."
            };

            var (prompt, allImages) = promptBuilder.BuildImageGradingPrompt(question, "data:image/png;base64,STUDENT...", null);

            Assert.Equal(3, allImages.Count);
            Assert.Contains("Attached Images Reference Guide:", prompt);
            Assert.Contains("Image 1: QUESTION DIAGRAM", prompt);
            Assert.Contains("Image 2: REFERENCE SOLUTION DIAGRAM", prompt);
            Assert.Contains("Image 3: STUDENT SUBMITTED HANDWRITTEN SOLUTION", prompt);
        }

        [Fact]
        public void CleanAndParseGradeResponse_StripsThinkTagsAndParsesJson()
        {
            var parser = new AiResponseParserService();
            string raw = @"<think>Some reasoning here</think>
```json
{
  ""PointsAwarded"": 5,
  ""CriteriaBreakdown"": ""### Breakdown by Criteria\n* Part 1: 5 - Excellent"",
  ""Feedback"": ""Great job""
}
```";

            var result = parser.CleanAndParseGradeResponse(raw);

            Assert.NotNull(result);
            Assert.Equal(5, result!.PointsAwarded);
            Assert.Contains("Great job", result.Feedback);
        }

        [Fact]
        public void SanitizeJsonMath_EscapesSolitaryBackslashes()
        {
            var parser = new AiResponseParserService();
            string unescaped = @"{ ""questionText"": ""Solve \frac{1}{2} + \theta"" }";

            var sanitized = parser.SanitizeJsonMath(unescaped);

            Assert.Contains(@"\\frac", sanitized);
            Assert.Contains(@"\\theta", sanitized);
        }

        [Fact]
        public void ParseOcrResponse_ExtractsAndSanitizesFields()
        {
            var parser = new AiResponseParserService();
            var logger = NullLogger.Instance;
            string rawOcr = @"```json
{
  ""questionText"": ""Find \( x^2 + 5x = 0 \)"",
  ""optionA"": ""\( x = 0 \)"",
  ""optionB"": ""\( x = 5 \)""
}
```";

            var result = parser.ParseOcrResponse(rawOcr, logger);

            Assert.NotNull(result);
            Assert.Contains("x^2 + 5x = 0", result.Text);
            Assert.Equal(@"\(x = 0\)", result.OptionA);
        }
    }
}
