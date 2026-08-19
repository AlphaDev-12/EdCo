using System.Collections.Generic;
using System.Threading.Tasks;
using EdCo.Core.Entities;

namespace EdCo.Core.Interfaces
{
    public interface IAiApiKeyService
    {
        Task<string> GetActiveProviderAsync();
        Task<bool> SetActiveProviderAsync(string provider);
        Task<string> GetActiveKeyAsync(string? provider = null);
        Task<List<AiApiKey>> GetAllKeysAsync(string provider = "Groq");
        Task<AiApiKey> AddKeyAsync(string label, string rawKey, bool setAsActive = true, string provider = "Groq", string? createdBy = null);
        Task<bool> SetActiveKeyAsync(int keyId, string provider = "Groq");
        Task<bool> DeleteKeyAsync(int keyId, string? deletedBy = null);
        Task<bool> TestApiKeyAsync(string provider, string rawKey);
        Task<bool> TestGroqKeyAsync(string rawKey);
        Task RecordKeyUsageAsync(string provider = "Groq");
        void InvalidateCache(string provider = "Groq");
    }
}
