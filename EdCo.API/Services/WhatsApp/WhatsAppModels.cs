using System.Collections.Generic;

namespace EdCo.API.Services.WhatsApp
{
    public class InteractiveButton
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
    }

    public class InteractiveListSection
    {
        public string Title { get; set; } = string.Empty;
        public List<InteractiveListRow> Rows { get; set; } = new List<InteractiveListRow>();
    }

    public class InteractiveListRow
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class WhatsAppSettings
    {
        public string AccessToken { get; set; } = string.Empty;
        public string PhoneNumberId { get; set; } = string.Empty;
        public string BusinessAccountId { get; set; } = string.Empty;
        public string AppSecret { get; set; } = string.Empty;
        public string VerifyToken { get; set; } = "edco_verify_token_123";
        public string ApiVersion { get; set; } = "v23.0";

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(AccessToken) &&
            !string.IsNullOrWhiteSpace(PhoneNumberId);
    }

    public class WhatsAppSendResult
    {
        public bool Success { get; set; }
        public string? MessageId { get; set; }
        public string? Error { get; set; }
        public int StatusCode { get; set; }
    }
}
