using System.Net.Http;

namespace EdCo.Core.Interfaces
{
    public interface IAiProviderStrategy
    {
        string ProviderName { get; }
        string GetTestEndpointUrl(string? configuredBaseUrl);
        HttpRequestMessage CreateTestPingRequest(string rawKey, string? configuredBaseUrl);
    }
}
