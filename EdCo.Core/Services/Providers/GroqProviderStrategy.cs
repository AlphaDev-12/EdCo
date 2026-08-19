using System.Net.Http;
using System.Net.Http.Headers;
using EdCo.Core.Interfaces;

namespace EdCo.Core.Services.Providers
{
    public class GroqProviderStrategy : IAiProviderStrategy
    {
        public string ProviderName => "Groq";

        public string GetTestEndpointUrl(string? configuredBaseUrl)
        {
            var baseUrl = string.IsNullOrWhiteSpace(configuredBaseUrl) ? "https://api.groq.com/openai/v1" : configuredBaseUrl;
            return $"{baseUrl.TrimEnd('/')}/models";
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
