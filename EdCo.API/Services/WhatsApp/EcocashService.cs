using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EdCo.API.Services.WhatsApp
{
    public class EcocashPaymentResult
    {
        public bool Success { get; set; }
        public string? TransactionId { get; set; }
        public string? ReferenceNumber { get; set; }
        public string? PollUrl { get; set; }
        public string? Error { get; set; }
        public string Status { get; set; } = "pending";
    }

    public interface IEcocashService
    {
        bool IsConfigured { get; }
        Task<EcocashPaymentResult> InitiatePaymentAsync(string mobileNumber, decimal amount, string reference, string description);
        Task<EcocashPaymentResult> CheckPaymentStatusAsync(string pollUrl);
    }

    /// <summary>
    /// Direct Ecocash Mobile Payment integration via Paynow's Remote Mobile Express Transaction API.
    /// Sends a direct USSD push prompt to the customer's EcoCash handset without requiring a web browser.
    /// </summary>
    public class EcocashService : IEcocashService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EcocashService> _logger;

        private readonly string _integrationId;
        private readonly string _integrationKey;
        private readonly string _returnUrl;
        private readonly string _resultUrl;

        public EcocashService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<EcocashService> logger)
        {
            _httpClient = httpClientFactory.CreateClient("Ecocash");
            _configuration = configuration;
            _logger = logger;

            var paynowConfig = _configuration.GetSection("Paynow");
            _integrationId = paynowConfig["IntegrationId"] ?? string.Empty;
            _integrationKey = paynowConfig["IntegrationKey"] ?? string.Empty;
            _returnUrl = paynowConfig["ReturnUrl"] ?? "http://localhost:5075/api/v1/subscription/paynow-return";
            _resultUrl = paynowConfig["ResultUrl"] ?? "http://localhost:5075/api/v1/subscription/paynow-result";
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_integrationId) && !string.IsNullOrWhiteSpace(_integrationKey);

        /// <summary>
        /// Initiates an EcoCash express USSD push via Paynow Remote Transaction API.
        /// </summary>
        public async Task<EcocashPaymentResult> InitiatePaymentAsync(string mobileNumber, decimal amount, string reference, string description)
        {
            if (!IsConfigured)
            {
                return new EcocashPaymentResult
                {
                    Success = false,
                    Error = "Paynow credentials (IntegrationId / IntegrationKey) are not configured."
                };
            }

            var localPhone = FormatToLocalEconetPhone(mobileNumber);
            if (string.IsNullOrEmpty(localPhone))
            {
                return new EcocashPaymentResult
                {
                    Success = false,
                    Error = "Invalid EcoCash mobile number. Please provide a valid Zimbabwe EcoCash number (e.g., 077... or 078...)."
                };
            }

            try
            {
                // Step 1: Initiate Transaction with Paynow
                var initDict = new Dictionary<string, string>
                {
                    { "id", _integrationId },
                    { "reference", reference },
                    { "amount", amount.ToString("0.00") },
                    { "additionalinfo", description },
                    { "returnurl", _returnUrl },
                    { "resulturl", _resultUrl },
                    { "authemail", "guardian-ecocash@edco.ac.zw" },
                    { "status", "Message" }
                };

                // Hash generation (Concat all values + integrationKey -> SHA512)
                var hashBuilder = new StringBuilder();
                foreach (var kvp in initDict)
                {
                    hashBuilder.Append(kvp.Value);
                }
                hashBuilder.Append(_integrationKey);

                using (var sha512 = SHA512.Create())
                {
                    var hashBytes = sha512.ComputeHash(Encoding.UTF8.GetBytes(hashBuilder.ToString()));
                    initDict["hash"] = Convert.ToHexString(hashBytes).ToUpper();
                }

                _logger.LogInformation("Initiating Paynow EcoCash transaction for {Phone}, Ref: {Ref}, Amount: {Amount}", localPhone, reference, amount);

                var initResponse = await _httpClient.PostAsync("https://www.paynow.co.zw/interface/initiatetransaction", new FormUrlEncodedContent(initDict));
                var initResponseString = await initResponse.Content.ReadAsStringAsync();
                var initResult = ParsePaynowResponse(initResponseString);

                if (!initResult.TryGetValue("status", out var initStatus) || !initStatus.Equals("Ok", StringComparison.OrdinalIgnoreCase))
                {
                    var errorDetails = initResult.TryGetValue("error", out var err) ? err : initResponseString;
                    _logger.LogWarning("Paynow initiation failed for {Ref}: {Error}", reference, errorDetails);
                    return new EcocashPaymentResult
                    {
                        Success = false,
                        Error = $"Paynow initiation failed: {errorDetails}"
                    };
                }

                var pollUrl = initResult.TryGetValue("pollurl", out var poll) ? poll : null;
                var instructionsUrl = initResult.TryGetValue("instructions", out var instUrl) ? instUrl : null;

                if (string.IsNullOrWhiteSpace(instructionsUrl))
                {
                    instructionsUrl = "https://www.paynow.co.zw/interface/remotetransaction";
                }

                // Step 2: Push EcoCash USSD Prompt to Mobile Handset via Express Remote Transaction
                var remoteDict = new Dictionary<string, string>
                {
                    { "phone", localPhone },
                    { "method", "ecocash" },
                    { "authemail", "guardian-ecocash@edco.ac.zw" },
                    { "status", "Message" }
                };

                var remoteResponse = await _httpClient.PostAsync(instructionsUrl, new FormUrlEncodedContent(remoteDict));
                var remoteResponseString = await remoteResponse.Content.ReadAsStringAsync();
                var remoteResult = ParsePaynowResponse(remoteResponseString);

                _logger.LogInformation("Paynow EcoCash USSD push response for {Ref}: {Response}", reference, remoteResponseString);

                bool isSuccess = remoteResult.TryGetValue("status", out var remoteStatus) &&
                                 (remoteStatus.Equals("Ok", StringComparison.OrdinalIgnoreCase) || remoteStatus.Equals("Pending", StringComparison.OrdinalIgnoreCase));

                return new EcocashPaymentResult
                {
                    Success = isSuccess,
                    Status = isSuccess ? "pending" : "failed",
                    ReferenceNumber = reference,
                    PollUrl = pollUrl,
                    TransactionId = pollUrl,
                    Error = isSuccess ? null : (remoteResult.TryGetValue("error", out var remoteErr) ? remoteErr : remoteResponseString)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during Paynow EcoCash initiation for {Phone}", mobileNumber);
                return new EcocashPaymentResult
                {
                    Success = false,
                    Error = $"Payment exception: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Polls transaction status from Paynow poll URL.
        /// </summary>
        public async Task<EcocashPaymentResult> CheckPaymentStatusAsync(string pollUrl)
        {
            if (string.IsNullOrWhiteSpace(pollUrl))
            {
                return new EcocashPaymentResult { Success = false, Error = "Poll URL is required." };
            }

            try
            {
                var response = await _httpClient.PostAsync(pollUrl, null);
                var responseString = await response.Content.ReadAsStringAsync();
                var dict = ParsePaynowResponse(responseString);

                var status = dict.TryGetValue("status", out var s) ? s : "unknown";
                bool isPaid = status.Equals("Paid", StringComparison.OrdinalIgnoreCase);

                return new EcocashPaymentResult
                {
                    Success = isPaid,
                    Status = status.ToLower(),
                    PollUrl = pollUrl
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception checking Paynow status from {PollUrl}", pollUrl);
                return new EcocashPaymentResult { Success = false, Error = ex.Message };
            }
        }

        private static string FormatToLocalEconetPhone(string rawNumber)
        {
            if (string.IsNullOrWhiteSpace(rawNumber)) return string.Empty;
            var digits = new string(rawNumber.Where(char.IsDigit).ToArray());

            if (digits.StartsWith("263") && digits.Length >= 12)
            {
                digits = "0" + digits.Substring(3);
            }
            else if (!digits.StartsWith("0") && digits.Length == 9)
            {
                digits = "0" + digits;
            }

            return digits;
        }

        private static Dictionary<string, string> ParsePaynowResponse(string response)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(response)) return dict;

            var parts = response.Split('&');
            foreach (var part in parts)
            {
                var kvp = part.Split('=');
                if (kvp.Length == 2)
                {
                    dict[Uri.UnescapeDataString(kvp[0])] = Uri.UnescapeDataString(kvp[1]);
                }
            }
            return dict;
        }
    }
}
