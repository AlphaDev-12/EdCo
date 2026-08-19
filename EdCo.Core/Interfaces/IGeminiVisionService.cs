using System.Threading.Tasks;

namespace EdCo.Core.Interfaces
{
    public interface IGeminiVisionService
    {
        Task<string> ExtractMathFromImageAsync(string base64Image, string prompt, string? appUserId = null);
        Task<string> ExtractMathFromImagesAsync(IEnumerable<string> base64Images, string prompt, string? appUserId = null);
        Task<string> GenerateContentAsync(string prompt, string? appUserId = null);
    }
}
