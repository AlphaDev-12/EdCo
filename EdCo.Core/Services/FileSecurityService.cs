using System.Diagnostics;
using EdCo.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace EdCo.Core.Services
{
    public class FileSecurityService : IFileSecurityService
    {
        private readonly ILogger<FileSecurityService> _logger;

        // Known magic byte signatures for common document and image extensions
        private static readonly Dictionary<string, byte[][]> FileSignatures = new(StringComparer.OrdinalIgnoreCase)
        {
            { ".pdf",  new[] { new byte[] { 0x25, 0x50, 0x44, 0x46 } } }, // %PDF
            { ".docx", new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 } } }, // PK.. (ZIP archive)
            { ".doc",  new[] { new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 } } },
            { ".png",  new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } } },
            { ".jpg",  new[] { new byte[] { 0xFF, 0xD8, 0xFF } } },
            { ".jpeg", new[] { new byte[] { 0xFF, 0xD8, 0xFF } } },
            { ".gif",  new[] { new byte[] { 0x47, 0x49, 0x46, 0x38 } } }
        };

        public FileSecurityService(ILogger<FileSecurityService> logger)
        {
            _logger = logger;
        }

        public async Task<(bool IsValid, string ErrorMessage)> ValidateAndScanAsync(IFormFile file, string[] allowedExtensions, long maxByteSize)
        {
            if (file == null || file.Length == 0)
            {
                return (false, "Uploaded file is empty.");
            }

            // 1. Max File Size Limit
            if (file.Length > maxByteSize)
            {
                var maxMb = maxByteSize / (1024 * 1024);
                _logger.LogWarning("File upload rejected: Size {Size} bytes exceeds limit of {MaxMb}MB.", file.Length, maxMb);
                return (false, $"File size exceeds the maximum limit of {maxMb}MB.");
            }

            // 2. Extension Verification & Sanitization
            var rawFileName = file.FileName;
            var ext = Path.GetExtension(rawFileName).ToLowerInvariant();

            if (string.IsNullOrEmpty(ext) || !allowedExtensions.Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogWarning("File upload rejected: Extension '{Ext}' is not permitted.", ext);
                return (false, $"File extension '{ext}' is not permitted.");
            }

            // Reject dangerous executable/script extensions regardless
            var dangerousExtensions = new[] { ".exe", ".dll", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".asp", ".aspx", ".php", ".py", ".sh", ".cgi", ".msi" };
            if (dangerousExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                _logger.LogWarning("File upload rejected: Potentially dangerous extension '{Ext}'.", ext);
                return (false, "Uploaded file type is restricted for security reasons.");
            }

            // 3. Magic Byte Signature Verification
            if (FileSignatures.TryGetValue(ext, out var signatures))
            {
                using var stream = file.OpenReadStream();
                using var reader = new BinaryReader(stream);
                var headerBytes = reader.ReadBytes(16);

                bool matchesSignature = signatures.Any(sig => headerBytes.Take(sig.Length).SequenceEqual(sig));

                if (!matchesSignature)
                {
                    _logger.LogWarning("File upload rejected: Magic bytes for file '{FileName}' do not match expected signature for '{Ext}'.", file.FileName, ext);
                    return (false, "File contents do not match the expected extension (spoofed header detected).");
                }
            }

            // 4. Windows Defender Malware Scan (if running on Windows VPS)
            var (isClean, scanErrorMessage) = await ScanWithWindowsDefenderAsync(file);
            if (!isClean)
            {
                _logger.LogError("File upload rejected: Malware/threat detected during Windows Defender scan for file '{FileName}'.", file.FileName);
                return (false, scanErrorMessage);
            }

            return (true, string.Empty);
        }

        private async Task<(bool IsClean, string ErrorMessage)> ScanWithWindowsDefenderAsync(IFormFile file)
        {
            // Windows Defender CLI executable path on Windows OS
            var mpCmdPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Windows Defender", "MpCmdRun.exe");

            if (!File.Exists(mpCmdPath))
            {
                _logger.LogInformation("Windows Defender CLI ('{Path}') not present on this host. Skipping antivirus subprocess pass.", mpCmdPath);
                return (true, string.Empty);
            }

            var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}");

            try
            {
                using (var fs = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await file.CopyToAsync(fs);
                }

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = mpCmdPath,
                    Arguments = $"-Scan -ScanType 3 -File \"{tempFilePath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processStartInfo);
                if (process == null)
                {
                    _logger.LogWarning("Could not start MpCmdRun.exe process for antivirus scan.");
                    return (true, string.Empty);
                }

                await process.WaitForExitAsync();

                // MpCmdRun return codes:
                // 0: No threat found
                // 2: Threat found
                if (process.ExitCode == 2)
                {
                    var stdout = await process.StandardOutput.ReadToEndAsync();
                    _logger.LogError("Windows Defender detected threat in uploaded file. MpCmdRun output: {Stdout}", stdout);
                    return (false, "Malware threat detected in uploaded document.");
                }
                else if (process.ExitCode != 0)
                {
                    _logger.LogWarning("MpCmdRun exit code: {Code}.", process.ExitCode);
                }

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while running Windows Defender malware scan on temp file.");
                // Fail open with warning to avoid breaking uploads if MpCmdRun permissions are restricted
                return (true, string.Empty);
            }
            finally
            {
                if (File.Exists(tempFilePath))
                {
                    try
                    {
                        File.Delete(tempFilePath);
                    }
                    catch
                    {
                        // Ignore cleanup exceptions
                    }
                }
            }
        }
    }
}
