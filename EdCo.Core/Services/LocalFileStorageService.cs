using EdCo.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EdCo.Core.Services
{
    public class LocalFileStorageService : ILocalFileStorageService
    {
        private readonly string _baseStoragePath;
        private readonly ILogger<LocalFileStorageService> _logger;
        private readonly FileExtensionContentTypeProvider _contentTypeProvider;

        public LocalFileStorageService(IConfiguration configuration, ILogger<LocalFileStorageService> logger)
        {
            _logger = logger;
            _contentTypeProvider = new FileExtensionContentTypeProvider();

            // Configured storage path (e.g., C:\EdCoData\Uploads or App_Data/Uploads)
            var configPath = configuration["Storage:LocalUploadPath"];
            if (!string.IsNullOrWhiteSpace(configPath))
            {
                _baseStoragePath = Path.IsPathRooted(configPath)
                    ? configPath
                    : Path.Combine(Directory.GetCurrentDirectory(), configPath);
            }
            else
            {
                // Default secure directory outside wwwroot
                _baseStoragePath = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "Uploads");
            }

            if (!Directory.Exists(_baseStoragePath))
            {
                Directory.CreateDirectory(_baseStoragePath);
                _logger.LogInformation("Created secure local storage directory at '{Path}'.", _baseStoragePath);
            }
        }

        public async Task<string> SaveFileAsync(IFormFile file, string subFolder)
        {
            var targetFolder = GetSanitizedFolderPath(subFolder);
            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }

            var originalFileName = Path.GetFileName(file.FileName);
            var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
            var uniqueFileName = $"{Guid.NewGuid():N}_{SanitizeFileName(Path.GetFileNameWithoutExtension(originalFileName))}{extension}";

            var filePath = Path.Combine(targetFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await file.CopyToAsync(stream);
            }

            _logger.LogInformation("Saved file '{UniqueName}' to secure local path '{Path}'.", uniqueFileName, filePath);
            return uniqueFileName;
        }

        public (Stream stream, string contentType) GetFileStream(string fileName, string subFolder)
        {
            var filePath = GetPhysicalPath(fileName, subFolder);

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File '{fileName}' was not found in storage.", filePath);
            }

            if (!_contentTypeProvider.TryGetContentType(filePath, out var contentType))
            {
                contentType = "application/octet-stream";
            }

            var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return (stream, contentType);
        }

        public string GetPhysicalPath(string fileName, string subFolder)
        {
            var safeSubfolder = GetSanitizedFolderPath(subFolder);
            var safeFileName = Path.GetFileName(fileName); // Prevents path traversal via fileName

            var combinedPath = Path.Combine(safeSubfolder, safeFileName);
            var fullPath = Path.GetFullPath(combinedPath);

            // Path Traversal Security Guard: Ensure resolved path resides strictly within the storage directory
            if (!fullPath.StartsWith(_baseStoragePath, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Path traversal attempt blocked for filename '{FileName}'.", fileName);
                throw new UnauthorizedAccessException("Invalid file path specified.");
            }

            return fullPath;
        }

        public bool FileExists(string fileName, string subFolder)
        {
            try
            {
                var filePath = GetPhysicalPath(fileName, subFolder);
                return File.Exists(filePath);
            }
            catch
            {
                return false;
            }
        }

        public Task<bool> DeleteFileAsync(string fileName, string subFolder)
        {
            try
            {
                var filePath = GetPhysicalPath(fileName, subFolder);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogInformation("Deleted file '{FileName}' from subfolder '{SubFolder}'.", fileName, subFolder);
                    return Task.FromResult(true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file '{FileName}' from '{SubFolder}'.", fileName, subFolder);
            }
            return Task.FromResult(false);
        }

        private string GetSanitizedFolderPath(string subFolder)
        {
            var safeSub = SanitizeFileName(subFolder);
            return Path.Combine(_baseStoragePath, safeSub);
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "default";
            var invalidChars = Path.GetInvalidFileNameChars().Concat(Path.GetInvalidPathChars()).Concat(new[] { '/', '\\', '.' }).ToArray();
            var cleaned = new string(name.Where(c => !invalidChars.Contains(c)).ToArray());
            return string.IsNullOrWhiteSpace(cleaned) ? "file" : cleaned;
        }
    }
}
