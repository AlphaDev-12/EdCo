using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using EdCo.API.DTOs;
using EdCo.Core.Utilities;

namespace EdCo.API.Services
{
    public class AiResponseParserService : IAiResponseParserService
    {
        public AiGradeResponseDto? CleanAndParseGradeResponse(string raw)
        {
            var text = raw;

            // Strip all <think>...</think> blocks
            text = Regex.Replace(text, @"<think>[\s\S]*?</think>", "", RegexOptions.IgnoreCase).Trim();

            // Remove markdown code fences
            text = Regex.Replace(text, @"^```(?:json)?\s*", "", RegexOptions.Multiline).Trim();
            text = Regex.Replace(text, @"\s*```\s*$", "", RegexOptions.Multiline).Trim();

            // Collapse double curly braces
            text = text.Replace("{{", "{").Replace("}}", "}");

            // Try to extract the first JSON object using regex
            var jsonMatch = Regex.Match(text, @"\{[^{}]*""PointsAwarded""[^{}]*\}", RegexOptions.IgnoreCase);
            if (jsonMatch.Success)
            {
                text = jsonMatch.Value;
            }

            // Sanitize invalid JSON escape sequences
            text = Regex.Replace(text, @"\\(?![""\\\/bfnrtu])", @"\\");

            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var result = JsonSerializer.Deserialize<AiGradeResponseDto>(text, options);
                return result;
            }
            catch
            {
                return null;
            }
        }

        public string CleanJsonResponse(string raw)
        {
            var text = Regex.Replace(raw, @"<think>[\s\S]*?</think>", "", RegexOptions.IgnoreCase).Trim();
            text = Regex.Replace(text, @"^```(?:json)?\s*", "", RegexOptions.Multiline).Trim();
            text = Regex.Replace(text, @"\s*```\s*$", "", RegexOptions.Multiline).Trim();
            text = text.Replace("{{", "{").Replace("}}", "}");
            text = SanitizeJsonMath(text);
            return text;
        }

        public string SanitizeJsonMath(string jsonText)
        {
            if (string.IsNullOrWhiteSpace(jsonText)) return jsonText;

            var mathNCommands = new[] { "neq", "ne\\b", "nu", "nabla", "neg", "norm", "notin", "natural", "not\\b", "nsubseteq", "nsupseteq", "nrightarrow", "nleftarrow", "number", "normal", "newcommand" };
            foreach (var cmd in mathNCommands)
            {
                jsonText = Regex.Replace(jsonText, $@"(?<!\\)\\n(?={cmd})", @"\\n", RegexOptions.IgnoreCase);
            }

            jsonText = Regex.Replace(jsonText, @"(?<!\\)\\(?![""\\\/n]|r\\n|u[0-9a-fA-F]{4})", @"\\\\");

            return jsonText;
        }

        public List<RubricCriterionDto>? ParseRubricCriteria(string raw, ILogger logger)
        {
            var cleanedText = raw.Trim();
            cleanedText = Regex.Replace(cleanedText, @"<think>[\s\S]*?</think>", "", RegexOptions.IgnoreCase).Trim();
            cleanedText = Regex.Replace(cleanedText, @"^```(?:json)?\s*", "", RegexOptions.Multiline).Trim();
            cleanedText = Regex.Replace(cleanedText, @"\s*```\s*$", "", RegexOptions.Multiline).Trim();
            cleanedText = cleanedText.Replace("{{", "{").Replace("}}", "}");

            int firstBracket = cleanedText.IndexOf('[');
            int lastBracket = -1;
            if (firstBracket >= 0)
            {
                int openCount = 0;
                for (int i = firstBracket; i < cleanedText.Length; i++)
                {
                    if (cleanedText[i] == '[') openCount++;
                    else if (cleanedText[i] == ']')
                    {
                        openCount--;
                        if (openCount == 0)
                        {
                            lastBracket = i;
                            break;
                        }
                    }
                }
            }
            if (firstBracket >= 0 && lastBracket > firstBracket)
            {
                cleanedText = cleanedText.Substring(firstBracket, lastBracket - firstBracket + 1);
            }

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            List<RubricCriterionDto>? criteria = null;

            try
            {
                var escapedJson = SanitizeJsonMath(cleanedText);
                criteria = JsonSerializer.Deserialize<List<RubricCriterionDto>>(escapedJson, options);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Initial JSON deserialize failed for rubric text. Attempting sanitization fallback.");
                var sanitizedJson = Regex.Replace(cleanedText, @"\\(?![""\\\/bfnrtu])", "");
                try
                {
                    criteria = JsonSerializer.Deserialize<List<RubricCriterionDto>>(sanitizedJson, options);
                }
                catch (Exception ex2)
                {
                    logger.LogError(ex2, "Fallback JSON deserialize also failed for rubric.");
                }
            }

            return criteria;
        }

        public ExtractedOcrResultDto ParseOcrResponse(string rawReply, ILogger logger)
        {
            var cleanedReply = Regex.Replace(rawReply, @"<think>[\s\S]*?</think>", "", RegexOptions.IgnoreCase).Trim();
            cleanedReply = Regex.Replace(cleanedReply, @"^```(?:json)?\s*", "", RegexOptions.Multiline).Trim();
            cleanedReply = Regex.Replace(cleanedReply, @"\s*```\s*$", "", RegexOptions.Multiline).Trim();

            var jsonMatch = Regex.Match(cleanedReply, @"\{[\s\S]*\}");
            if (jsonMatch.Success)
            {
                cleanedReply = jsonMatch.Value;
            }

            string extractedQuestionText = "";
            string optionA = "";
            string optionB = "";
            string optionC = "";
            string optionD = "";
            string correctOption = "";

            try
            {
                var escapedJson = SanitizeJsonMath(cleanedReply);
                using var parsed = JsonDocument.Parse(escapedJson);
                var root = parsed.RootElement;

                if (root.TryGetProperty("questionText", out var qt)) extractedQuestionText = qt.GetString() ?? "";
                if (string.IsNullOrWhiteSpace(extractedQuestionText) && root.TryGetProperty("answerText", out var at)) extractedQuestionText = at.GetString() ?? "";
                if (string.IsNullOrWhiteSpace(extractedQuestionText) && root.TryGetProperty("referenceAnswer", out var ra)) extractedQuestionText = ra.GetString() ?? "";
                if (string.IsNullOrWhiteSpace(extractedQuestionText) && root.TryGetProperty("correctAnswer", out var ca)) extractedQuestionText = ca.GetString() ?? "";
                if (string.IsNullOrWhiteSpace(extractedQuestionText) && root.TryGetProperty("answer", out var ans)) extractedQuestionText = ans.GetString() ?? "";

                if (root.TryGetProperty("optionA", out var oa)) optionA = oa.GetString() ?? "";
                if (root.TryGetProperty("optionB", out var ob)) optionB = ob.GetString() ?? "";
                if (root.TryGetProperty("optionC", out var oc)) optionC = oc.GetString() ?? "";
                if (root.TryGetProperty("optionD", out var od)) optionD = od.GetString() ?? "";
                if (root.TryGetProperty("correctOption", out var co)) correctOption = co.GetString() ?? "";
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to parse OCR response as JSON. Attempting regex extraction.");

                var qMatch = Regex.Match(cleanedReply, @"""questionText""\s*:\s*""((?:[^""\\]|\\.)*)""\s*,\s*""optionA""");
                if (!qMatch.Success)
                {
                    qMatch = Regex.Match(cleanedReply, @"""questionText""\s*:\s*""((?:[^""\\]|\\.)*)""");
                }

                if (qMatch.Success)
                {
                    extractedQuestionText = qMatch.Groups[1].Value.Replace(@"\n", "\n").Replace(@"\""", "\"").Replace(@"\\", @"\");
                }
                else
                {
                    extractedQuestionText = Regex.Replace(cleanedReply, @"^\{[\s\S]*?""questionText""\s*:\s*""", "");
                    extractedQuestionText = Regex.Replace(extractedQuestionText, @"""\s*,\s*""optionA""[\s\S]*\}$", "");
                    extractedQuestionText = Regex.Replace(extractedQuestionText, @"\}\s*$", "");
                }
            }

            extractedQuestionText = LaTeXSanitizer.Sanitize(extractedQuestionText);
            optionA = LaTeXSanitizer.Sanitize(optionA);
            optionB = LaTeXSanitizer.Sanitize(optionB);
            optionC = LaTeXSanitizer.Sanitize(optionC);
            optionD = LaTeXSanitizer.Sanitize(optionD);

            return new ExtractedOcrResultDto
            {
                Text = extractedQuestionText,
                OptionA = optionA,
                OptionB = optionB,
                OptionC = optionC,
                OptionD = optionD,
                CorrectOption = correctOption
            };
        }
    }
}
