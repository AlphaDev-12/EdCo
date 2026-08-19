using EdCo.API.Services;
using EdCo.Core.Interfaces;
using Moq;
using Xunit;

namespace EdCo.Tests
{
    public class GradingJobQueueTests
    {
        private readonly Mock<ICacheService> _mockCache;
        private readonly GradingJobQueue _queue;

        public GradingJobQueueTests()
        {
            _mockCache = new Mock<ICacheService>();
            _queue = new GradingJobQueue(_mockCache.Object);
        }

        [Fact]
        public async Task EnqueueJobAsync_ValidJob_WritesToQueueAndCache()
        {
            // Arrange
            var job = new GradingJobItem
            {
                JobId = "job_test_123",
                StudentUserId = "user_456",
                QuizId = 1,
                Questions = new List<GradingQuestionTask>
                {
                    new GradingQuestionTask { QuestionId = 10, StudentAnswer = "Answer text", IsVision = false }
                },
                Status = "Enqueued",
                CreatedAt = DateTime.UtcNow
            };

            // Act
            await _queue.EnqueueJobAsync(job);

            // Assert
            _mockCache.Verify(c => c.SetAsync(
                It.Is<string>(k => k == "grading_job:job_test_123"),
                It.Is<GradingJobItem>(j => j.JobId == "job_test_123"),
                It.IsAny<TimeSpan?>()), Times.Once);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var dequeuedJob = await _queue.DequeueJobAsync(cts.Token);
            Assert.NotNull(dequeuedJob);
            Assert.Equal("job_test_123", dequeuedJob.JobId);
        }

        [Fact]
        public async Task EnqueueJobAsync_NullJob_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _queue.EnqueueJobAsync(null!).AsTask());
        }

        [Fact]
        public async Task GetJobStatusAsync_CallsCacheService()
        {
            // Arrange
            var expectedJob = new GradingJobItem { JobId = "job_789", Status = "Completed" };
            _mockCache.Setup(c => c.GetAsync<GradingJobItem>("grading_job:job_789"))
                .ReturnsAsync(expectedJob);

            // Act
            var result = await _queue.GetJobStatusAsync("job_789");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Completed", result.Status);
        }
    }
}
