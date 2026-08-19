using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using EdCo.Core.Interfaces;
using EdCo.Core.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace EdCo.API.Services
{
    public class GeminiVisionService : IGeminiVisionService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GeminiVisionService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;

        public GeminiVisionService(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiVisionService> logger, IServiceScopeFactory scopeFactory)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        private async Task<(string provider, string baseUrl, string visionModel, string textModel)> ResolveActiveProviderInfoAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var apiKeyService = scope.ServiceProvider.GetService<IAiApiKeyService>();
            
            string provider = apiKeyService != null 
                ? await apiKeyService.GetActiveProviderAsync() 
                : (_configuration["AiSettings:ActiveProvider"] ?? "DeepInfra");

            string baseUrl, visionModel, textModel;

            if (string.Equals(provider, "DeepInfra", StringComparison.OrdinalIgnoreCase))
            {
                baseUrl = _configuration["DeepInfra:BaseUrl"] ?? "https://api.deepinfra.com/v1/openai";
                textModel = _configuration["DeepInfra:ModelName"] ?? "meta-llama/Meta-Llama-3.1-8B-Instruct-Turbo";
                visionModel = _configuration["DeepInfra:VisionModelName"] ?? "meta-llama/Llama-4-Scout-17B-16E-Instruct";
            }
            else
            {
                provider = "Groq";
                baseUrl = _configuration["Groq:BaseUrl"] ?? "https://api.groq.com/openai/v1";
                textModel = _configuration["Groq:ModelName"] ?? "openai/gpt-oss-20b";
                visionModel = _configuration["Groq:VisionModelName"] ?? "qwen/qwen3.6-27b";
            }

            return (provider, baseUrl, visionModel, textModel);
        }

        /// <summary>
        /// Processes an image with a text prompt using the active vision model.
        /// </summary>
        public async Task<string> ExtractMathFromImageAsync(string base64Image, string prompt, string? appUserId = null)
        {
            var (provider, baseUrl, visionModel, _) = await ResolveActiveProviderInfoAsync();
            _logger.LogInformation("Processing image with {Provider} vision model: {Model}", provider, visionModel);

            base64Image = ResizeImageBase64(base64Image);

            var apiUrl = $"{baseUrl.TrimEnd('/')}/chat/completions";

            var textPart = new Dictionary<string, object>
            {
                { "type", "text" },
                { "text", prompt }
            };

            var imagePart = new Dictionary<string, object>
            {
                { "type", "image_url" },
                { "image_url", new Dictionary<string, string> { { "url", $"data:image/jpeg;base64,{base64Image}" } } }
            };

            var message = new Dictionary<string, object>
            {
                { "role", "user" },
                { "content", new List<Dictionary<string, object>> { textPart, imagePart } }
            };

            var requestBody = new Dictionary<string, object>
            {
                { "model", visionModel },
                { "messages", new List<Dictionary<string, object>> { message } },
                { "max_tokens", 4096 }
            };

            if (string.Equals(provider, "Groq", StringComparison.OrdinalIgnoreCase))
            {
                requestBody["reasoning_effort"] = "none";
            }

            return await SendAiRequestAsync(provider, apiUrl, requestBody, appUserId);
        }

        /// <summary>
        /// Processes multiple images with a text prompt using the active vision model.
        /// Stitches multiple images into a single composite visual canvas to ensure 100% reliable vision parsing with Llama 4 Scout.
        /// </summary>
        public async Task<string> ExtractMathFromImagesAsync(IEnumerable<string> base64Images, string prompt, string? appUserId = null)
        {
            var imageList = base64Images?.Where(i => !string.IsNullOrWhiteSpace(i)).ToList() ?? new List<string>();
            if (imageList.Count == 0)
            {
                return await GenerateContentAsync(prompt, appUserId);
            }
            if (imageList.Count == 1)
            {
                return await ExtractMathFromImageAsync(imageList[0], prompt, appUserId);
            }

            _logger.LogInformation("Combining {Count} images into composite visual canvas for Llama vision model", imageList.Count);
            string combinedBase64 = CombineImagesBase64(imageList);
            return await ExtractMathFromImageAsync(combinedBase64, prompt, appUserId);
        }

        /// <summary>
        /// Generates text-only content using the active text model.
        /// </summary>
        public async Task<string> GenerateContentAsync(string prompt, string? appUserId = null)
        {
            var (provider, baseUrl, _, textModel) = await ResolveActiveProviderInfoAsync();
            _logger.LogInformation("Processing text with {Provider} text model: {Model}", provider, textModel);

            var apiUrl = $"{baseUrl.TrimEnd('/')}/chat/completions";

            var requestBody = new Dictionary<string, object>
            {
                { "model", textModel },
                { "messages", new List<Dictionary<string, object>> {
                    new Dictionary<string, object>
                    {
                        { "role", "user" },
                        { "content", prompt }
                    }
                }},
                { "max_tokens", 4096 }
            };

            return await SendAiRequestAsync(provider, apiUrl, requestBody, appUserId);
        }

        private async Task<string> SendAiRequestAsync(string provider, string url, object requestBody, string? appUserId = null, int maxRetries = 3)
        {
            using var scope = _scopeFactory.CreateScope();
            var apiKeyService = scope.ServiceProvider.GetService<IAiApiKeyService>();
            string apiKey = apiKeyService != null 
                ? await apiKeyService.GetActiveKeyAsync(provider) 
                : (_configuration[$"{provider}:ApiKey"] ?? string.Empty);

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, url);
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                    request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                    var response = await _httpClient.SendAsync(request);
                    var responseString = await response.Content.ReadAsStringAsync();

                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        var delay = TimeSpan.FromSeconds(10);
                        if (response.Headers.RetryAfter?.Delta != null)
                        {
                            delay = response.Headers.RetryAfter.Delta.Value;
                        }
                        else
                        {
                            var match = System.Text.RegularExpressions.Regex.Match(responseString, @"in ([\d\.]+)s");
                            if (match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double seconds))
                            {
                                delay = TimeSpan.FromSeconds(seconds + 0.5);
                            }
                        }

                        if (delay.TotalSeconds > 15)
                        {
                            delay = TimeSpan.FromSeconds(15);
                        }

                        _logger.LogWarning("{Provider} API rate limited (429): {ResponseBody}. Retrying in {Delay:F1}s (attempt {Attempt}/{Max})", provider, responseString, delay.TotalSeconds, attempt + 1, maxRetries);

                        await LogAiErrorAsync(
                            new HttpRequestException($"{provider} API 429 Rate Limit: {responseString}"),
                            provider: provider,
                            logLevel: attempt == maxRetries - 1 ? "Error" : "Warning",
                            customMessage: $"{provider} API Rate Limit (429) hit on attempt {attempt + 1}/{maxRetries}: {responseString}");

                        if (attempt < maxRetries - 1)
                        {
                            await Task.Delay(delay);
                            continue;
                        }
                        else
                        {
                            throw new GroqRateLimitException(
                                $"{provider} API rate limit exceeded. Please try again in {(int)delay.TotalSeconds} seconds.",
                                retryAfterSeconds: (int)delay.TotalSeconds,
                                responseBody: responseString);
                        }
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogError("{Provider} API error {Status}: {Body}", provider, (int)response.StatusCode, responseString);
                        var httpEx = new HttpRequestException($"{provider} API returned {(int)response.StatusCode}: {responseString}");
                        await LogAiErrorAsync(httpEx, provider: provider, logLevel: "Error", customMessage: $"{provider} API HTTP {(int)response.StatusCode}: {responseString}");
                        throw httpEx;
                    }

                    using var document = JsonDocument.Parse(responseString);
                    var messageProp = document.RootElement.GetProperty("choices")[0].GetProperty("message");
                    
                    var text = messageProp.TryGetProperty("content", out var contentProp) ? contentProp.GetString() : null;
                    if (string.IsNullOrWhiteSpace(text) && messageProp.TryGetProperty("reasoning_content", out var reasoningProp))
                    {
                        text = reasoningProp.GetString();
                        _logger.LogInformation("{Provider} content was empty, fallback to reasoning_content.", provider);
                    }

                    if (string.IsNullOrWhiteSpace(text))
                    {
                        _logger.LogWarning("{Provider} returned empty text content. Full raw response: {ResponseString}", provider, responseString);
                    }

                    await TrackTokenUsageAsync(document.RootElement, appUserId);
                    if (apiKeyService != null)
                    {
                        _ = apiKeyService.RecordKeyUsageAsync(provider);
                    }

                    return text ?? string.Empty;
                }
                catch (GroqRateLimitException) { throw; }
                catch (HttpRequestException) { throw; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing {Provider} response on attempt {Attempt}", provider, attempt + 1);
                    await LogAiErrorAsync(ex, provider: provider, logLevel: "Error", customMessage: $"{provider} API call exception on attempt {attempt + 1}: {ex.Message}");
                    if (attempt == maxRetries - 1) throw;
                }
            }

            var maxRetriesEx = new GroqRateLimitException($"Max retries exceeded for {provider} API", retryAfterSeconds: 15);
            await LogAiErrorAsync(maxRetriesEx, provider: provider, logLevel: "Error");
            throw maxRetriesEx;
        }

        private async Task LogAiErrorAsync(Exception exception, string provider = "DeepInfra", string logLevel = "Error", string? customMessage = null)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var errorLogService = scope.ServiceProvider.GetService<IErrorLogService>();
                if (errorLogService != null)
                {
                    await errorLogService.LogErrorAsync(
                        exception,
                        source: provider,
                        httpContext: null,
                        logLevel: logLevel,
                        customMessage: customMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record {Provider} error log into database.", provider);
            }
        }

        private async Task TrackTokenUsageAsync(JsonElement root, string? appUserId)
        {
            try
            {
                if (root.TryGetProperty("usage", out var usageProp))
                {
                    var promptTokens = usageProp.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 0;
                    var completionTokens = usageProp.TryGetProperty("completion_tokens", out var ct) ? ct.GetInt32() : 0;
                    var totalTokens = usageProp.TryGetProperty("total_tokens", out var tt) ? tt.GetInt32() : 0;

                    var modelUsed = root.TryGetProperty("model", out var modelProp) ? modelProp.GetString() : null;

                    decimal cost = EdCo.Core.Utilities.AiCostCalculator.CalculateCost(modelUsed, promptTokens, completionTokens);

                    using var scope = _scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<EdCo.Core.Data.EdCoDbContext>();

                    var log = new EdCo.Core.Entities.AiInteractionLog
                    {
                        AppUserId = appUserId,
                        PromptTokens = promptTokens,
                        CompletionTokens = completionTokens,
                        TotalTokens = totalTokens,
                        ModelUsed = modelUsed,
                        Cost = cost
                    };

                    dbContext.AiInteractionLogs.Add(log);
                    await dbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to track AI token usage in GeminiVisionService.");
            }
        }

        private string ResizeImageBase64(string base64Data)
        {
            try
            {
                var cleanBase64 = base64Data.Contains(",") ? base64Data.Split(',')[1] : base64Data;
                byte[] imageBytes = Convert.FromBase64String(cleanBase64);
                
                using var image = Image.Load(imageBytes);
                
                // Maximum dimension of 1280px for high-clarity OCR with Llama vision models
                const int MaxDimension = 1280;
                
                if (image.Width <= MaxDimension && image.Height <= MaxDimension)
                {
                    // Preserve original uncompressed image crispness
                    return cleanBase64;
                }

                var options = new ResizeOptions
                {
                    Size = new Size(MaxDimension, MaxDimension),
                    Mode = ResizeMode.Max
                };
                
                image.Mutate(x => x.Resize(options));
                
                using var ms = new MemoryStream();
                image.Save(ms, new JpegEncoder { Quality = 90 });
                return Convert.ToBase64String(ms.ToArray());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resize image, returning original base64 data");
                return base64Data.Contains(",") ? base64Data.Split(',')[1] : base64Data;
            }
        }

        private string CombineImagesBase64(List<string> base64Images)
        {
            if (base64Images == null || base64Images.Count == 0) return string.Empty;
            if (base64Images.Count == 1) return ResizeImageBase64(base64Images[0]);

            try
            {
                var loadedImages = new List<Image>();
                foreach (var b64 in base64Images)
                {
                    var clean = b64.Contains(",") ? b64.Split(',')[1] : b64;
                    byte[] bytes = Convert.FromBase64String(clean);
                    loadedImages.Add(Image.Load(bytes));
                }

                int maxWidth = loadedImages.Max(img => img.Width);
                int totalHeight = loadedImages.Sum(img => img.Height);

                using var canvas = new Image<Rgba32>(maxWidth, totalHeight, Color.White);
                canvas.Mutate(ctx =>
                {
                    int currentY = 0;
                    foreach (var img in loadedImages)
                    {
                        ctx.DrawImage(img, new Point(0, currentY), 1f);
                        currentY += img.Height;
                    }
                });

                foreach (var img in loadedImages)
                {
                    img.Dispose();
                }

                const int MaxDimension = 1280;
                if (canvas.Width > MaxDimension || canvas.Height > MaxDimension)
                {
                    canvas.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Size = new Size(MaxDimension, MaxDimension),
                        Mode = ResizeMode.Max
                    }));
                }

                using var ms = new MemoryStream();
                canvas.Save(ms, new JpegEncoder { Quality = 85 });
                return Convert.ToBase64String(ms.ToArray());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to combine images, falling back to first image");
                return ResizeImageBase64(base64Images[0]);
            }
        }
    }
}
