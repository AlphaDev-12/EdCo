using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using EdCo.API.Services.WhatsApp;
using EdCo.Core.Data;
using EdCo.Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EdCo.API.Controllers
{
    /// <summary>
    /// WhatsApp Cloud API webhook controller for the Guardian chatbot.
    /// Handles Meta webhook verification and incoming message routing.
    /// Adapted from EduPay's CommunicationWebhooksController.
    /// </summary>
    [Route("api/v1/whatsapp/webhook")]
    [ApiController]
    [AllowAnonymous]
    public class WhatsAppWebhookController : ControllerBase
    {
        private readonly ILogger<WhatsAppWebhookController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IGuardianWhatsAppBotService _botService;
        private readonly UserManager<AppUser> _userManager;
        private readonly EdCoDbContext _dbContext;

        public WhatsAppWebhookController(
            ILogger<WhatsAppWebhookController> logger,
            IConfiguration configuration,
            IGuardianWhatsAppBotService botService,
            UserManager<AppUser> userManager,
            EdCoDbContext dbContext)
        {
            _logger = logger;
            _configuration = configuration;
            _botService = botService;
            _userManager = userManager;
            _dbContext = dbContext;
        }

        /// <summary>
        /// Meta WhatsApp webhook verification (GET).
        /// Meta calls this when you first register the webhook URL.
        /// </summary>
        [HttpGet]
        public IActionResult VerifyWebhook(
            [FromQuery(Name = "hub.mode")] string mode,
            [FromQuery(Name = "hub.verify_token")] string token,
            [FromQuery(Name = "hub.challenge")] string challenge)
        {
            var verifyToken = _configuration["MetaWhatsApp:VerifyToken"] ?? "edco_verify_token_123";

            if (mode == "subscribe" && token == verifyToken)
            {
                _logger.LogInformation("WhatsApp webhook verified successfully.");
                return Ok(int.Parse(challenge));
            }

            _logger.LogWarning("WhatsApp webhook verification failed. Mode={Mode}, TokenMatch={Match}", mode, token == verifyToken);
            return Forbid();
        }

        /// <summary>
        /// Meta WhatsApp webhook receiver (POST).
        /// Handles incoming messages and routes to the guardian bot engine.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ReceiveMessage()
        {
            var appSecret = _configuration["MetaWhatsApp:AppSecret"];
            var signature = Request.Headers["X-Hub-Signature-256"].ToString();

            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();

            // Validate HMAC signature if AppSecret is configured
            if (!string.IsNullOrEmpty(appSecret) && !string.IsNullOrEmpty(signature))
            {
                var expectedSignature = "sha256=" + ComputeHmacSha256(body, appSecret);
                if (signature != expectedSignature)
                {
                    _logger.LogWarning("Invalid WhatsApp webhook signature.");
                    return Unauthorized();
                }
            }

            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                // Meta webhook payload structure:
                // { "entry": [{ "changes": [{ "value": { "messages": [...] } }] }] }
                if (root.TryGetProperty("entry", out var entries))
                {
                    foreach (var entry in entries.EnumerateArray())
                    {
                        if (!entry.TryGetProperty("changes", out var changes)) continue;

                        foreach (var change in changes.EnumerateArray())
                        {
                            if (!change.TryGetProperty("value", out var value)) continue;

                            // Route incoming messages to the bot
                            if (value.TryGetProperty("messages", out var messages))
                            {
                                foreach (var message in messages.EnumerateArray())
                                {
                                    LogIncomingMessage(message);
                                    await ProcessMessageForBotAsync(message);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing WhatsApp webhook payload: {Body}", body.Length > 500 ? body[..500] : body);
            }

            // Always return 200 to Meta to acknowledge receipt
            return Ok();
        }

        /// <summary>
        /// Ecocash payment callback endpoint.
        /// Called by the Ecocash API when a payment is completed.
        /// </summary>
        [HttpPost("ecocash-callback")]
        public async Task<IActionResult> EcocashCallback()
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();

            _logger.LogInformation("Ecocash callback received: {Body}", body.Length > 500 ? body[..500] : body);

            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                var status = root.TryGetProperty("status", out var statusProp)
                    ? statusProp.GetString()?.ToLower() : null;

                var reference = root.TryGetProperty("reference", out var refProp)
                    ? refProp.GetString() : null;

                if (status == "paid" || status == "completed" || status == "success")
                {
                    if (!string.IsNullOrEmpty(reference) && reference.StartsWith("EDCO-SUB-"))
                    {
                        // Extract student ID from reference: EDCO-SUB-{userId}-{ticks}
                        var parts = reference.Split('-');
                        if (parts.Length >= 3)
                        {
                            var studentUserId = parts[2];
                            
                            var user = await _userManager.FindByIdAsync(studentUserId);
                            
                            if (user != null)
                            {
                                var gradeLevel = await _dbContext.GradeLevels.FindAsync(user.GradeLevelId);
                                int durationDays = gradeLevel?.SubscriptionDurationDays > 0 ? gradeLevel.SubscriptionDurationDays : 90;

                                user.IsSubscribed = true;
                                user.SubscriptionEndDate = DateTime.UtcNow.AddDays(durationDays);
                                await _userManager.UpdateAsync(user);

                                _logger.LogInformation("Ecocash payment confirmed. Student {StudentId} subscribed until {EndDate}",
                                    studentUserId, user.SubscriptionEndDate);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Ecocash callback.");
            }

            return Ok();
        }

        private void LogIncomingMessage(JsonElement message)
        {
            try
            {
                var from = message.TryGetProperty("from", out var fromProp) ? fromProp.GetString() : "unknown";
                var type = message.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : "unknown";
                var text = "";
                if (type == "text" && message.TryGetProperty("text", out var textObj))
                {
                    text = textObj.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? "" : "";
                }

                _logger.LogInformation("Incoming WhatsApp message from {From}: Type={Type}, Text={Text}",
                    from, type, text.Length > 100 ? text[..100] + "..." : text);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not log incoming WhatsApp message.");
            }
        }

        private async Task ProcessMessageForBotAsync(JsonElement message)
        {
            try
            {
                var from = message.TryGetProperty("from", out var fromProp) ? fromProp.GetString() : null;
                var type = message.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;

                if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(type)) return;

                string payload = "";
                string? interactiveId = null;

                if (type == "text" && message.TryGetProperty("text", out var textObj))
                {
                    payload = textObj.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? "" : "";
                }
                else if (type == "interactive" && message.TryGetProperty("interactive", out var intObj))
                {
                    var interactiveType = intObj.TryGetProperty("type", out var intTypeProp) ? intTypeProp.GetString() : "";

                    if (interactiveType == "button_reply" && intObj.TryGetProperty("button_reply", out var btnReply))
                    {
                        interactiveId = btnReply.TryGetProperty("id", out var idProp) ? idProp.GetString() : "";
                        payload = btnReply.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "" : "";
                    }
                    else if (interactiveType == "list_reply" && intObj.TryGetProperty("list_reply", out var listReply))
                    {
                        interactiveId = listReply.TryGetProperty("id", out var idProp) ? idProp.GetString() : "";
                        payload = listReply.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "" : "";
                    }
                }

                await _botService.ProcessIncomingMessageAsync(from, type, payload, interactiveId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error routing incoming message to guardian bot engine.");
            }
        }

        private string ComputeHmacSha256(string data, string secret)
        {
            var keyBytes = Encoding.UTF8.GetBytes(secret);
            var dataBytes = Encoding.UTF8.GetBytes(data);

            using var hmac = new HMACSHA256(keyBytes);
            var hashBytes = hmac.ComputeHash(dataBytes);

            return Convert.ToHexString(hashBytes).ToLower();
        }
    }
}
