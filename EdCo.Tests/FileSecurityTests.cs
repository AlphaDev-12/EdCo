using System.IO;
using System.Text;
using System.Threading.Tasks;
using EdCo.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EdCo.Tests
{
    public class FileSecurityTests
    {
        [Fact]
        public async Task ValidateAndScanAsync_DangerousExtension_ReturnsInvalid()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<FileSecurityService>>();
            var service = new FileSecurityService(mockLogger.Object);

            var stream = new MemoryStream(Encoding.UTF8.GetBytes("echo 'malicious payload'"));
            var formFile = new FormFile(stream, 0, stream.Length, "file", "script.exe");

            // Act
            var (isValid, errorMessage) = await service.ValidateAndScanAsync(formFile, new[] { ".exe", ".pdf" }, 10_000_000);

            // Assert
            Assert.False(isValid);
            Assert.Contains("restricted", errorMessage);
        }

        [Fact]
        public async Task ValidateAndScanAsync_SpoofedMagicBytes_ReturnsInvalid()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<FileSecurityService>>();
            var service = new FileSecurityService(mockLogger.Object);

            // Fake text content posing as .pdf
            var stream = new MemoryStream(Encoding.UTF8.GetBytes("This is plain text content posing as PDF"));
            var formFile = new FormFile(stream, 0, stream.Length, "file", "fake.pdf");

            // Act
            var (isValid, errorMessage) = await service.ValidateAndScanAsync(formFile, new[] { ".pdf" }, 10_000_000);

            // Assert
            Assert.False(isValid);
            Assert.Contains("spoofed header", errorMessage);
        }

        [Fact]
        public async Task ValidateAndScanAsync_ValidPdfHeader_ReturnsValid()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<FileSecurityService>>();
            var service = new FileSecurityService(mockLogger.Object);

            // True PDF magic bytes %PDF-1.4
            var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0x0A, 0x25, 0xE2, 0xE3, 0xCF, 0xD3 };
            var stream = new MemoryStream(pdfBytes);
            var formFile = new FormFile(stream, 0, stream.Length, "file", "document.pdf");

            // Act
            var (isValid, errorMessage) = await service.ValidateAndScanAsync(formFile, new[] { ".pdf" }, 10_000_000);

            // Assert
            Assert.True(isValid);
            Assert.Empty(errorMessage);
        }
    }
}
