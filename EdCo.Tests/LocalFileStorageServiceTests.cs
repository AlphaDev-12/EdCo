using System.Text;
using EdCo.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EdCo.Tests
{
    public class LocalFileStorageServiceTests : IDisposable
    {
        private readonly string _tempTestDir;
        private readonly LocalFileStorageService _storageService;

        public LocalFileStorageServiceTests()
        {
            _tempTestDir = Path.Combine(Path.GetTempPath(), "EdCo_Test_Uploads_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempTestDir);

            var inMemorySettings = new Dictionary<string, string?>
            {
                { "Storage:LocalUploadPath", _tempTestDir }
            };

            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            var mockLogger = new Mock<ILogger<LocalFileStorageService>>();
            _storageService = new LocalFileStorageService(config, mockLogger.Object);
        }

        [Fact]
        public async Task SaveFileAsync_ValidFile_SavesSuccessfully()
        {
            // Arrange
            var content = "Test file contents for EdCo storage test.";
            var fileName = "test_document.txt";
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            var formFile = new FormFile(stream, 0, stream.Length, "file", fileName);

            // Act
            var savedFileName = await _storageService.SaveFileAsync(formFile, "assignments");

            // Assert
            Assert.False(string.IsNullOrWhiteSpace(savedFileName));
            Assert.True(_storageService.FileExists(savedFileName, "assignments"));

            var (readStream, contentType) = _storageService.GetFileStream(savedFileName, "assignments");
            using var reader = new StreamReader(readStream);
            var readContent = await reader.ReadToEndAsync();

            Assert.Equal(content, readContent);
            Assert.Equal("text/plain", contentType);
        }

        [Fact]
        public void GetPhysicalPath_PathTraversalAttempt_SanitizesPathSafely()
        {
            // Act
            var path = _storageService.GetPhysicalPath("../secret_config.json", "../assignments");

            // Assert
            Assert.StartsWith(_tempTestDir, path, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith("secret_config.json", path);
            Assert.DoesNotContain("..", path);
        }

        [Fact]
        public async Task DeleteFileAsync_ExistingFile_DeletesFile()
        {
            // Arrange
            var content = "Content to be deleted.";
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            var formFile = new FormFile(stream, 0, stream.Length, "file", "to_delete.txt");
            var savedFileName = await _storageService.SaveFileAsync(formFile, "temp");

            // Act
            var deleteResult = await _storageService.DeleteFileAsync(savedFileName, "temp");

            // Assert
            Assert.True(deleteResult);
            Assert.False(_storageService.FileExists(savedFileName, "temp"));
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempTestDir))
                {
                    Directory.Delete(_tempTestDir, true);
                }
            }
            catch
            {
                // Ignore cleanup exceptions
            }
        }
    }
}
