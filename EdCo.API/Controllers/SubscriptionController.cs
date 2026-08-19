using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using EdCo.Core.Entities;
using EdCo.Core.Data;

namespace EdCo.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class SubscriptionController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly UserManager<AppUser> _userManager;
        private readonly EdCoDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;

        public SubscriptionController(
            IConfiguration configuration,
            UserManager<AppUser> userManager,
            EdCoDbContext context,
            IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _userManager = userManager;
            _context = context;
            _httpClientFactory = httpClientFactory;
        }

        [HttpPost("initiate")]
        public async Task<IActionResult> InitiateSubscription()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return Unauthorized();

            var paynowConfig = _configuration.GetSection("Paynow");
            var integrationId = paynowConfig["IntegrationId"] ?? string.Empty;
            var integrationKey = paynowConfig["IntegrationKey"] ?? string.Empty;
            var returnUrl = paynowConfig["ReturnUrl"] ?? string.Empty;
            var resultUrl = paynowConfig["ResultUrl"] ?? string.Empty;

            // Fetch dynamic TierPrice and SubscriptionDurationDays from GradeLevel
            var gradeLevel = await _context.GradeLevels.FindAsync(user.GradeLevelId);
            decimal amount = gradeLevel?.TierPrice ?? 0;
            int durationDays = gradeLevel?.SubscriptionDurationDays > 0 ? gradeLevel.SubscriptionDurationDays : 90;
            
            if (amount <= 0)
            {
                return BadRequest(new { success = false, message = "This grade level is free or invalid. No subscription required." });
            }

            string reference = $"SUB-{user.Id}-{DateTime.UtcNow.Ticks}";

            var dict = new Dictionary<string, string>
            {
                { "id", integrationId },
                { "reference", reference },
                { "amount", amount.ToString("0.00") },
                { "additionalinfo", $"EdCo Premium Subscription ({durationDays} Days)" },
                { "returnurl", returnUrl },
                { "resulturl", resultUrl },
                { "status", "Message" }
            };

            // Generate Hash
            string hashString = "";
            foreach (var kvp in dict)
            {
                hashString += kvp.Value;
            }
            hashString += integrationKey;

            using (var sha512 = SHA512.Create())
            {
                var hashBytes = sha512.ComputeHash(Encoding.UTF8.GetBytes(hashString));
                dict["hash"] = Convert.ToHexString(hashBytes).ToUpper();
            }

            // Send to Paynow via IHttpClientFactory (prevents socket exhaustion)
            using var client = _httpClientFactory.CreateClient("Paynow");
            var content = new FormUrlEncodedContent(dict);
            var response = await client.PostAsync("https://www.paynow.co.zw/interface/initiatetransaction", content);

            var responseString = await response.Content.ReadAsStringAsync();
            var responseDict = ParsePaynowResponse(responseString);

            if (responseDict.TryGetValue("status", out var status) && status == "Ok")
            {
                return Ok(new
                {
                    success = true,
                    browserUrl = responseDict["browserurl"],
                    pollUrl = responseDict["pollurl"],
                    reference = reference
                });
            }

            return BadRequest(new { success = false, message = "Failed to initiate Paynow transaction.", details = responseString });
        }

        [AllowAnonymous]
        [HttpPost("paynow-result")]
        public async Task<IActionResult> PaynowResult()
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            var dict = ParsePaynowResponse(body);

            if (dict.TryGetValue("status", out var status) && status == "Paid")
            {
                var reference = dict.TryGetValue("reference", out var r) ? r : "";
                if (reference.Contains("SUB-"))
                {
                    var parts = reference.Split('-');
                    // Reference formats: SUB-{userId}-{ticks} OR EDCO-SUB-{userId}-{ticks}
                    string? userId = null;
                    if (reference.StartsWith("EDCO-SUB-") && parts.Length >= 3)
                    {
                        userId = parts[2];
                    }
                    else if (parts.Length >= 2)
                    {
                        userId = parts[1];
                    }

                    if (!string.IsNullOrEmpty(userId))
                    {
                        var user = await _userManager.FindByIdAsync(userId);
                        if (user != null)
                        {
                            var gradeLevel = await _context.GradeLevels.FindAsync(user.GradeLevelId);
                            int durationDays = gradeLevel?.SubscriptionDurationDays > 0 ? gradeLevel.SubscriptionDurationDays : 90;
                            
                            user.IsSubscribed = true;
                            user.SubscriptionEndDate = DateTime.UtcNow.AddDays(durationDays);
                            await _userManager.UpdateAsync(user);
                        }
                    }
                }
            }
            return Ok();
        }
        [AllowAnonymous]
        [HttpGet("paynow-return")]
        public IActionResult PaynowReturn()
        {
            var html = @"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset='utf-8'>
                <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                <title>Payment Complete</title>
                <style>
                    body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #0F0F1A; color: #fff; display: flex; flex-direction: column; align-items: center; justify-content: center; height: 100vh; margin: 0; }
                    .card { background-color: #1A1A2E; padding: 40px; border-radius: 16px; text-align: center; border: 1px solid #2A2A4A; max-width: 90%; }
                    h1 { color: #4ADE80; margin-bottom: 16px; }
                    p { color: #A0A0C0; line-height: 1.6; font-size: 18px; }
                </style>
            </head>
            <body>
                <div class='card'>
                    <h1>Payment Processed 🎉</h1>
                    <p>Your transaction has been processed by Paynow.</p>
                    <p><strong>You can now safely close this browser window and return to the EdCo App.</strong></p>
                </div>
            </body>
            </html>";
            
            return Content(html, "text/html");
        }
        
        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return Unauthorized();
            var gradeLevel = await _context.GradeLevels.FindAsync(user.GradeLevelId);

            return Ok(new {
                success = true,
                isSubscribed = user.IsSubscribed,
                endDate = user.SubscriptionEndDate,
                tierPrice = gradeLevel?.TierPrice ?? 0,
                subscriptionDurationDays = gradeLevel?.SubscriptionDurationDays > 0 ? gradeLevel.SubscriptionDurationDays : 90
            });
        }

        [HttpPost("verify")]
        public async Task<IActionResult> VerifyPayment([FromBody] VerifyRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.PollUrl))
            {
                return BadRequest(new { success = false, message = "No Poll URL provided." });
            }

            using var client = _httpClientFactory.CreateClient("Paynow");
            var response = await client.PostAsync(request.PollUrl, null);
            var responseString = await response.Content.ReadAsStringAsync();
            var dict = ParsePaynowResponse(responseString);

            if (dict.TryGetValue("status", out var status) && status == "Paid")
            {
                if (!user.IsSubscribed)
                {
                    var gradeLevel = await _context.GradeLevels.FindAsync(user.GradeLevelId);
                    int durationDays = gradeLevel?.SubscriptionDurationDays > 0 ? gradeLevel.SubscriptionDurationDays : 90;

                    user.IsSubscribed = true;
                    user.SubscriptionEndDate = DateTime.UtcNow.AddDays(durationDays);
                    await _userManager.UpdateAsync(user);
                }
                
                return Ok(new { success = true, isSubscribed = true, endDate = user.SubscriptionEndDate });
            }

            return Ok(new { success = true, isSubscribed = false, message = "Transaction is still pending or was cancelled." });
        }

        private Dictionary<string, string> ParsePaynowResponse(string response)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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

    public class VerifyRequest
    {
        public string PollUrl { get; set; } = string.Empty;
    }
}
