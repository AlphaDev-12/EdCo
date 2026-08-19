using System;

namespace EdCo.Core.Utilities
{
    /// <summary>
    /// Centralised token cost calculation for all AI providers.
    /// Eliminates duplication across GeminiVisionService, TutorController, etc.
    /// Pricing rates are per 1M tokens.
    /// </summary>
    public static class AiCostCalculator
    {
        /// <summary>
        /// Calculates the estimated USD cost for a given model based on token counts.
        /// </summary>
        public static decimal CalculateCost(string? modelUsed, int promptTokens, int completionTokens)
        {
            if (string.IsNullOrWhiteSpace(modelUsed))
                return 0m;

            // DeepInfra — Llama-4-Scout-17B
            if (modelUsed.Contains("Llama-4-Scout", StringComparison.OrdinalIgnoreCase))
            {
                return (promptTokens / 1_000_000m) * 0.10m
                     + (completionTokens / 1_000_000m) * 0.30m;
            }

            // DeepInfra — Llama-3.1-8B
            if (modelUsed.Contains("Llama-3.1-8B", StringComparison.OrdinalIgnoreCase)
                || modelUsed.Contains("3.1-8B", StringComparison.OrdinalIgnoreCase))
            {
                return (promptTokens / 1_000_000m) * 0.02m
                     + (completionTokens / 1_000_000m) * 0.04m;
            }

            // Groq — GPT-OSS-20B
            if (modelUsed.Contains("gpt-oss-20b", StringComparison.OrdinalIgnoreCase))
            {
                return (promptTokens / 1_000_000m) * 0.075m
                     + (completionTokens / 1_000_000m) * 0.30m;
            }

            // Groq — Qwen family
            if (modelUsed.Contains("qwen", StringComparison.OrdinalIgnoreCase))
            {
                return (promptTokens / 1_000_000m) * 0.60m
                     + (completionTokens / 1_000_000m) * 3.00m;
            }

            return 0m;
        }
    }
}
