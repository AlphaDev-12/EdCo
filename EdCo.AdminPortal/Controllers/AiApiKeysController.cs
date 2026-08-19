using System;
using System.Threading.Tasks;
using EdCo.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EdCo.AdminPortal.Controllers
{
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class AiApiKeysController : Controller
    {
        private readonly IAiApiKeyService _apiKeyService;
        private readonly IAuditLogService _auditLogService;

        public AiApiKeysController(
            IAiApiKeyService apiKeyService,
            IAuditLogService auditLogService)
        {
            _apiKeyService = apiKeyService;
            _auditLogService = auditLogService;
        }

        // GET: /AiApiKeys
        public async Task<IActionResult> Index(string? provider = null)
        {
            var activeProvider = await _apiKeyService.GetActiveProviderAsync();
            if (string.IsNullOrEmpty(provider))
            {
                provider = activeProvider;
            }

            var keys = await _apiKeyService.GetAllKeysAsync(provider);
            ViewBag.Provider = provider;
            ViewBag.ActiveProvider = activeProvider;
            return View(keys);
        }

        // POST: /AiApiKeys/SetActiveProvider
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetActiveProvider(string provider)
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                TempData["Error"] = "Invalid AI provider specified.";
                return RedirectToAction(nameof(Index));
            }

            bool success = await _apiKeyService.SetActiveProviderAsync(provider);
            if (success)
            {
                var adminUser = User.Identity?.Name ?? "Admin";
                await _auditLogService.LogAdminActionAsync(
                    action: "SetActiveAiProvider",
                    entityName: "AiSettings",
                    entityId: provider,
                    details: $"Switched primary system active AI provider to {provider}.",
                    userName: adminUser);

                TempData["Success"] = $"System active AI provider successfully switched to {provider}.";
            }
            else
            {
                TempData["Error"] = "Failed to switch active AI provider.";
            }

            return RedirectToAction(nameof(Index), new { provider });
        }

        // POST: /AiApiKeys/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string label, string rawKey, bool setAsActive = true, string provider = "DeepInfra")
        {
            if (string.IsNullOrWhiteSpace(rawKey))
            {
                TempData["Error"] = "API Key cannot be empty.";
                return RedirectToAction(nameof(Index), new { provider });
            }

            try
            {
                var adminUser = User.Identity?.Name ?? "Admin";
                var createdKey = await _apiKeyService.AddKeyAsync(label, rawKey, setAsActive, provider, createdBy: adminUser);

                await _auditLogService.LogAdminActionAsync(
                    action: "CreateAiApiKey",
                    entityName: "AiApiKey",
                    entityId: createdKey.Id.ToString(),
                    details: $"Added new encrypted {provider} API key labeled '{createdKey.Label}' (Masked: {createdKey.MaskedKey}). Active: {setAsActive}",
                    userName: adminUser);

                TempData["Success"] = $"API Key '{createdKey.Label}' successfully encrypted and saved.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed to save API key: {ex.Message}";
            }

            return RedirectToAction(nameof(Index), new { provider });
        }

        // POST: /AiApiKeys/SetActive
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetActive(int id, string provider = "DeepInfra")
        {
            bool success = await _apiKeyService.SetActiveKeyAsync(id, provider);
            if (success)
            {
                var adminUser = User.Identity?.Name ?? "Admin";
                await _auditLogService.LogAdminActionAsync(
                    action: "SetActiveAiApiKey",
                    entityName: "AiApiKey",
                    entityId: id.ToString(),
                    details: $"Activated {provider} API key ID {id}.",
                    userName: adminUser);

                TempData["Success"] = $"Active API key updated successfully.";
            }
            else
            {
                TempData["Error"] = "Failed to update active key.";
            }

            return RedirectToAction(nameof(Index), new { provider });
        }

        // POST: /AiApiKeys/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, string provider = "DeepInfra")
        {
            var adminUser = User.Identity?.Name ?? "Admin";
            bool success = await _apiKeyService.DeleteKeyAsync(id, deletedBy: adminUser);

            if (success)
            {
                await _auditLogService.LogAdminActionAsync(
                    action: "DeleteAiApiKey",
                    entityName: "AiApiKey",
                    entityId: id.ToString(),
                    details: $"Deleted {provider} API key ID {id}.",
                    userName: adminUser);

                TempData["Success"] = "API Key deleted successfully.";
            }
            else
            {
                TempData["Error"] = "Failed to delete API Key.";
            }

            return RedirectToAction(nameof(Index), new { provider });
        }

        // POST: /AiApiKeys/TestKey
        [HttpPost]
        public async Task<IActionResult> TestKey([FromBody] TestKeyRequestModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.RawKey))
            {
                return Json(new { success = false, message = "API Key is required for testing." });
            }

            string provider = string.IsNullOrWhiteSpace(model.Provider) ? "DeepInfra" : model.Provider;
            bool isValid = await _apiKeyService.TestApiKeyAsync(provider, model.RawKey);
            if (isValid)
            {
                return Json(new { success = true, message = $"{provider} API key connection test succeeded!" });
            }

            return Json(new { success = false, message = $"{provider} API key test failed. Please verify key validity or quota." });
        }
    }

    public class TestKeyRequestModel
    {
        public string RawKey { get; set; } = string.Empty;
        public string Provider { get; set; } = "DeepInfra";
    }
}
