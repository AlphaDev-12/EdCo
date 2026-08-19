using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace EdCo.Core.Utilities
{
    public static class LaTeXSanitizer
    {
        /// <summary>
        /// Sanitizes AI-generated or OCR-extracted LaTeX math strings to prevent formatting errors.
        /// Fixes literal '\t' tabs, escaped spaces '\ ', unescaped base subscripts, double backslashes,
        /// and unclosed math delimiters.
        /// </summary>
        public static string Sanitize(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            var text = input;

            // 1. Normalize line endings and unescape literal \n strings if present
            text = text.Replace(@"\r\n", "\n").Replace(@"\n", "\n").Replace("\r", "");

            // 2. Remove literal "\t" string representations and raw tab characters '\t'
            text = Regex.Replace(text, @"(?:\\t|\t)+", " ");

            // 3. Remove escaped spaces inside or outside LaTeX math (e.g. 11\ 001_2 -> 11 001_2)
            text = Regex.Replace(text, @"\\ ", " ");

            // 4. Collapse multiple backslashes before LaTeX math delimiters and commands
            // e.g. \\( -> \(, \\) -> \), \\[ -> \[, \\] -> \], \\frac -> \frac, \\sqrt -> \sqrt
            text = Regex.Replace(text, @"\\{2,}(\(|\)|\[|\]|frac|sqrt|theta|pi|alpha|beta|degree|times|div|pm|begin|end|sin|cos|tan|array|tabular|matrix|pmatrix|bmatrix|hline)", @"\$1");

            // 5. Strip redundant '$' wrappers on lines that already contain LaTeX delimiters \(...\) or \[...\]
            var linesArr = text.Split('\n');
            for (int i = 0; i < linesArr.Length; i++)
            {
                var line = linesArr[i];
                if (Regex.IsMatch(line, @"\\[\(\)\[\]]"))
                {
                    // Line already has LaTeX delimiters \(...\) or \[...\], remove conflicting $ signs
                    line = line.Replace("$", "");
                }
                else
                {
                    // Convert display $$ ... $$ into \[ ... \]
                    line = Regex.Replace(line, @"\$\$([\s\S]*?)\$\$", @"\[$1\]");
                    // Convert inline $ ... $ into \( ... \)
                    line = Regex.Replace(line, @"(?<!\\)\$([^\$\n]+?)(?<!\\)\$", @"\($1\)");
                }
                linesArr[i] = line;
            }
            text = string.Join("\n", linesArr);

            // 6. Merge preceding orphan digits/terms into LaTeX math delimiters (e.g. 11 \(001_{2}\) -> \(11 001_{2}\))
            text = Regex.Replace(text, @"\b([A-Za-z0-9]+)\s*\\\((.*?)\\\)", @"\($1 $2\)");

            // 7. Standardize subscript braces: e.g. 442_5 -> 442_{5}, x_12 -> x_{12}
            text = Regex.Replace(text, @"\b([A-Za-z0-9]+)_([0-9A-Za-z]+)\b", @"$1_{$2}");

            // 8. Wrap orphan subscript terms or unwrapped LaTeX commands in plain text (not already inside \(...\) or \[...\]) into \( ... \)
            var parts = Regex.Split(text, @"(\\\[[\s\S]*?\\\]|\\\([\s\S]*?\\\))");
            var sb = new StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (part.StartsWith(@"\(") || part.StartsWith(@"\["))
                {
                    sb.Append(part);
                }
                else
                {
                    // Auto-wrap subscript expressions like 442_{5} or 11001_{2} in plain text
                    var wrapped = Regex.Replace(part, @"\b([A-Za-z0-9]+_\{[A-Za-z0-9]+\})", @"\($1\)");

                    // Auto-wrap unwrapped \frac{...}{...} or \sqrt{...} sitting in plain text outside math delimiters
                    wrapped = Regex.Replace(wrapped, @"(?<!\\\()(\\(?:frac|sqrt|sin|cos|tan)\{[^{}]*\}(?:\{[^{}]*\})?)", @"\($1\)");

                    sb.Append(wrapped);
                }
            }
            text = sb.ToString();

            // 9. Ensure bracketed point mark allocations like \[2\] or \[3\] (intended for rubric generation) are unescaped back to [2], [3]
            text = Regex.Replace(text, @"\\\[(\d+)\\\]", @"[$1]");

            // 10. Fix unmatched inline delimiters per line
            var lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                // Balance unclosed curly braces { ... } from truncated LaTeX commands like \frac{...
                int openBrace = line.Count(c => c == '{');
                int closeBrace = line.Count(c => c == '}');
                if (openBrace > closeBrace)
                {
                    line += string.Concat(Enumerable.Repeat("}", openBrace - closeBrace));
                }

                int openParen = Regex.Matches(line, @"\\\(").Count;
                int closeParen = Regex.Matches(line, @"\\\)").Count;
                if (openParen > closeParen)
                {
                    line += " " + string.Concat(Enumerable.Repeat(@"\)", openParen - closeParen));
                }

                int openBracket = Regex.Matches(line, @"\\\[").Count;
                int closeBracket = Regex.Matches(line, @"\\\]").Count;
                if (openBracket > closeBracket)
                {
                    line += " " + string.Concat(Enumerable.Repeat(@"\]", openBracket - closeBracket));
                }

                lines[i] = line;
            }
            text = string.Join("\n", lines);

            // 11. Clean up extra spaces around math delimiters and duplicate spaces
            text = Regex.Replace(text, @"[ \t]{2,}", " ");
            text = Regex.Replace(text, @"\\\(\s+", @"\(");
            text = Regex.Replace(text, @"\s+\\\)", @"\)");

            return text.Trim();
        }
    }
}
