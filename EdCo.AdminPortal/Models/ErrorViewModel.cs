namespace EdCo.AdminPortal.Models;

public class ErrorViewModel
{
    public string? RequestId { get; set; }
    public int StatusCode { get; set; } = 500;
    public string Title { get; set; } = "An Error Occurred";
    public string Description { get; set; } = "An unexpected error occurred while processing your request.";

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
