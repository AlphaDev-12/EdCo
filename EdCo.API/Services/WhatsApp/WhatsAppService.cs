using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EdCo.API.Services.WhatsApp
{
    public interface IWhatsAppService
    {
        bool IsConfigured { get; }
        Task<WhatsAppSendResult> SendTextMessageAsync(string phoneNumber, string message);
        Task<WhatsAppSendResult> SendInteractiveButtonsAsync(string phoneNumber, string bodyText, List<InteractiveButton> buttons);
        Task<WhatsAppSendResult> SendInteractiveListAsync(string phoneNumber, string bodyText, string buttonText, List<InteractiveListSection> sections);
        string FormatPhoneNumber(string rawNumber);
    }

    public class WhatsAppService : IWhatsAppService
    {
        private readonly HttpClient _httpClient;
        private readonly WhatsAppSettings _settings;
        private readonly ILogger<WhatsAppService> _logger;

        public WhatsAppService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<WhatsAppService> logger)
        {
            _httpClient = httpClientFactory.CreateClient("WhatsApp");
            _settings = new WhatsAppSettings();
            configuration.GetSection("MetaWhatsApp").Bind(_settings);
            _logger = logger;
        }

        public bool IsConfigured => _settings.IsConfigured;

        private string ApiBaseUrl => $"https://graph.facebook.com/{_settings.ApiVersion}/{_settings.PhoneNumberId}";

        /// <summary>
        /// Format a phone number to E.164 (no '+', digits only). 
        /// Handles common Zimbabwe formats.
        /// </summary>
        public string FormatPhoneNumber(string rawNumber)
        {
            if (string.IsNullOrWhiteSpace(rawNumber)) return string.Empty;

            // Strip all non-digit characters
            var digits = new string(rawNumber.Where(char.IsDigit).ToArray());

            // If starts with 0, assume Zimbabwe local → prepend 263
            if (digits.StartsWith("0") && digits.Length >= 9)
            {
                digits = "263" + digits.Substring(1);
            }

            // If it doesn't start with a country code, assume Zimbabwe
            if (digits.Length <= 9)
            {
                digits = "263" + digits;
            }

            return digits;
        }

        public async Task<WhatsAppSendResult> SendTextMessageAsync(string phoneNumber, string message)
        {
            if (!_settings.IsConfigured)
            {
                return new WhatsAppSendResult
                {
                    Success = false,
                    Error = "WhatsApp API credentials are not configured. Set AccessToken and PhoneNumberId in appsettings.json under MetaWhatsApp."
                };
            }

            var formattedNumber = FormatPhoneNumber(phoneNumber);
            if (string.IsNullOrEmpty(formattedNumber))
            {
                return new WhatsAppSendResult { Success = false, Error = "Invalid phone number." };
            }

            var payload = new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to = formattedNumber,
                type = "text",
                text = new { preview_url = false, body = message }
            };

            return await SendRequestAsync(payload);
        }

        public async Task<WhatsAppSendResult> SendInteractiveButtonsAsync(string phoneNumber, string bodyText, List<InteractiveButton> buttons)
        {
            if (!_settings.IsConfigured)
                return new WhatsAppSendResult { Success = false, Error = "WhatsApp API credentials are not configured." };

            var formattedNumber = FormatPhoneNumber(phoneNumber);
            if (string.IsNullOrEmpty(formattedNumber))
                return new WhatsAppSendResult { Success = false, Error = "Invalid phone number." };

            var actionButtons = buttons.Select(b => new
            {
                type = "reply",
                reply = new { id = b.Id, title = b.Title }
            }).ToArray();

            var payload = new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to = formattedNumber,
                type = "interactive",
                interactive = new
                {
                    type = "button",
                    body = new { text = bodyText },
                    action = new { buttons = actionButtons }
                }
            };

            return await SendRequestAsync(payload);
        }

        public async Task<WhatsAppSendResult> SendInteractiveListAsync(string phoneNumber, string bodyText, string buttonText, List<InteractiveListSection> sections)
        {
            if (!_settings.IsConfigured)
                return new WhatsAppSendResult { Success = false, Error = "WhatsApp API credentials are not configured." };

            var formattedNumber = FormatPhoneNumber(phoneNumber);
            if (string.IsNullOrEmpty(formattedNumber))
                return new WhatsAppSendResult { Success = false, Error = "Invalid phone number." };

            var actionSections = sections.Select(s => new
            {
                title = s.Title,
                rows = s.Rows.Select(r => new
                {
                    id = r.Id,
                    title = r.Title,
                    description = r.Description
                }).ToArray()
            }).ToArray();

            var payload = new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to = formattedNumber,
                type = "interactive",
                interactive = new
                {
                    type = "list",
                    body = new { text = bodyText },
                    action = new
                    {
                        button = buttonText,
                        sections = actionSections
                    }
                }
            };

            return await SendRequestAsync(payload);
        }

        private async Task<WhatsAppSendResult> SendRequestAsync(object payload)
        {
            try
            {
                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });

                var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/messages");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.AccessToken);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger.LogDebug("Sending WhatsApp API request to {Url}", request.RequestUri);

                var response = await _httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                var result = new WhatsAppSendResult
                {
                    StatusCode = (int)response.StatusCode,
                    Success = response.IsSuccessStatusCode
                };

                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(responseBody);
                        if (doc.RootElement.TryGetProperty("messages", out var messages) && messages.GetArrayLength() > 0)
                        {
                            result.MessageId = messages[0].GetProperty("id").GetString();
                        }
                    }
                    catch { /* Parsing the ID is optional */ }

                    _logger.LogInformation("WhatsApp message sent successfully. WAMID: {MessageId}", result.MessageId);
                }
                else
                {
                    result.Error = $"HTTP {response.StatusCode}: {responseBody}";
                    _logger.LogWarning("WhatsApp API error: {StatusCode} - {Body}", response.StatusCode, responseBody);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception sending WhatsApp message");
                return new WhatsAppSendResult
                {
                    Success = false,
                    Error = $"Exception: {ex.Message}"
                };
            }
        }
    }
}
