using Microsoft.AspNetCore.Http;

namespace EdCo.Core.Interfaces
{
    public interface ILocalFileStorageService
    {
        Task<string> SaveFileAsync(IFormFile file, string subFolder);
        (Stream stream, string contentType) GetFileStream(string fileName, string subFolder);
        string GetPhysicalPath(string fileName, string subFolder);
        bool FileExists(string fileName, string subFolder);
        Task<bool> DeleteFileAsync(string fileName, string subFolder);
    }
}
