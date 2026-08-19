using EdCo.API.Services;
using Xunit;

namespace EdCo.Tests
{
    public class SubjectPromptClassificationTests
    {
        private readonly AiGradingPromptBuilder _promptBuilder = new();

        [Theory]
        [InlineData("Mathematics", true)]
        [InlineData("Pure Mathematics", true)]
        [InlineData("Physics", true)]
        [InlineData("Chemistry", true)]
        [InlineData("Accounting", true)]
        [InlineData("Combined Science", true)]
        [InlineData("Statistics", true)]
        [InlineData("Computer Science", true)]
        [InlineData(null, true)]
        [InlineData("", true)]
        public void IsQuantitativeSubject_IdentifiesQuantitativeSubjects(string? subjectName, bool expected)
        {
            var result = _promptBuilder.IsQuantitativeSubject(subjectName, isQuantitative: null);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("English Language", false)]
        [InlineData("English Literature", false)]
        [InlineData("History", false)]
        [InlineData("Geography", false)]
        [InlineData("Shona", false)]
        [InlineData("Ndebele", false)]
        [InlineData("Divinity & Religious Studies", false)]
        [InlineData("Heritage Studies", false)]
        [InlineData("Business Studies", false)]
        [InlineData("Commerce", false)]
        public void IsQuantitativeSubject_IdentifiesNonQuantitativeSubjects(string subjectName, bool expected)
        {
            var result = _promptBuilder.IsQuantitativeSubject(subjectName, isQuantitative: null);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void IsQuantitativeSubject_ExplicitFlagOverridesSubjectName()
        {
            var result = _promptBuilder.IsQuantitativeSubject("History", isQuantitative: true);
            Assert.True(result);

            var result2 = _promptBuilder.IsQuantitativeSubject("Mathematics", isQuantitative: false);
            Assert.False(result2);
        }
    }
}
