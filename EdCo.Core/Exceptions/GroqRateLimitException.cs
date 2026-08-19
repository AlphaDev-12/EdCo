using System;

namespace EdCo.Core.Exceptions
{
    public class GroqRateLimitException : Exception
    {
        public int RetryAfterSeconds { get; }
        public string? ModelName { get; }
        public string? ResponseBody { get; }

        public GroqRateLimitException(string message, int retryAfterSeconds = 10, string? modelName = null, string? responseBody = null)
            : base(message)
        {
            RetryAfterSeconds = retryAfterSeconds;
            ModelName = modelName;
            ResponseBody = responseBody;
        }

        public GroqRateLimitException(string message, Exception innerException, int retryAfterSeconds = 10, string? modelName = null, string? responseBody = null)
            : base(message, innerException)
        {
            RetryAfterSeconds = retryAfterSeconds;
            ModelName = modelName;
            ResponseBody = responseBody;
        }
    }
}
