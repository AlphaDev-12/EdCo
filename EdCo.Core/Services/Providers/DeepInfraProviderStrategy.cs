using System.Net.Http;
using System.Net.Http.Headers;
using EdCo.Core.Interfaces;

namespace EdCo.Core.Services.Providers
{
    public class DeepInfraProviderStrategy : IAiProviderStrategy
    {
        public string ProviderName => "DeepInfra";

        public string GetTestEndpointUrl(string? configuredBaseUrl)
        {
            return string.IsNullOrWhiteSpace(configuredBaseUrl) ? "https://api.deepinfra.com/v1/openai/models" : $"{configuredBaseUrl.TrimEnd('/')}/models";
        }

        public HttpRequestMessage CreateTestPingRequest(string rawKey, string? configuredBaseUrl)
        {
            var url = GetTestEndpointUrl(configuredBaseUrl);
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", rawKey.Trim());
            return request;
        }
    }
}
