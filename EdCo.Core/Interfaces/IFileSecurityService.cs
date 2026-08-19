using Microsoft.AspNetCore.Http;

namespace EdCo.Core.Interfaces
{
    public interface IFileSecurityService
    {
        Task<(bool IsValid, string ErrorMessage)> ValidateAndScanAsync(IFormFile file, string[] allowedExtensions, long maxByteSize);
    }
}
