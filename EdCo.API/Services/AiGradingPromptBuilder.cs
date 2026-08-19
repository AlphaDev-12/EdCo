using System.Text;
using System.Text.RegularExpressions;
using EdCo.API.DTOs;
using EdCo.Core.Entities;

namespace EdCo.API.Services
{
    public class AiGradingPromptBuilder : IAiGradingPromptBuilder
    {
        public string BuildQuestionGradingPrompt(QuizQuestion question, string studentAnswer)
        {
            var systemContext = GetCoreGradingPhilosophyPrompt(question);
            return systemContext + $"\n\nStudent Answer:\n{studentAnswer}";
        }

        public (string Prompt, List<string> AllImages) BuildImageGradingPrompt(QuizQuestion question, string base64Image, List<string>? base64Images)
        {
            var gradingPrompt = GetCoreGradingPhilosophyPrompt(question, isHandwrittenPhoto: true);

            var studentImages = new List<string>();
            if (base64Images != null && base64Images.Count > 0)
            {
                foreach (var img in base64Images)
                {
                    if (!string.IsNullOrWhiteSpace(img))
                    {
                        studentImages.Add(img.Contains(",") ? img.Split(',')[1] : img);
                    }
                }
            }
            else if (!string.IsNullOrWhiteSpace(base64Image))
            {
                studentImages.Add(base64Image.Contains(",") ? base64Image.Split(',')[1] : base64Image);
            }

            var allImages = new List<string>();
            var imageLabelGuide = new StringBuilder();
            imageLabelGuide.AppendLine("\n# Attached Images Reference Guide:");
            int imgIdx = 1;

            if (!string.IsNullOrWhiteSpace(question.ImageUrl))
            {
                var qBase64 = question.ImageUrl.Contains(",") ? question.ImageUrl.Split(',')[1] : question.ImageUrl;
                allImages.Add(qBase64);
                imageLabelGuide.AppendLine($"- Image {imgIdx}: QUESTION DIAGRAM");
                imgIdx++;
            }

            if (!string.IsNullOrWhiteSpace(question.CorrectAnswerImageUrl))
            {
                var refBase64 = question.CorrectAnswerImageUrl.Contains(",") ? question.CorrectAnswerImageUrl.Split(',')[1] : question.CorrectAnswerImageUrl;
                allImages.Add(refBase64);
                imageLabelGuide.AppendLine($"- Image {imgIdx}: REFERENCE SOLUTION DIAGRAM / MODEL ANSWER KEY");
                imgIdx++;
            }

            int pageNum = 1;
            foreach (var stImg in studentImages)
            {
                allImages.Add(stImg);
                imageLabelGuide.AppendLine($"- Image {imgIdx}: STUDENT SUBMITTED HANDWRITTEN SOLUTION (Page {pageNum})");
                imgIdx++;
                pageNum++;
            }

            var fullGradingPrompt = gradingPrompt + "\n" + imageLabelGuide.ToString();
            return (fullGradingPrompt, allImages);
        }

        public string BuildOcrPrompt(string? subjectName, bool? isQuantitative, bool isAnswerScan)
        {
            bool isQuant = IsQuantitativeSubject(subjectName, isQuantitative);
            if (!isQuant)
            {
                return isAnswerScan
                    ? @"You are an expert OCR transcription engine for past exam paper reference correct answers.
SUBJECT CONTEXT: Non-Quantitative Subject (" + (subjectName ?? "General Text") + @").

CRITICAL TEXT TRANSCRIPTION INSTRUCTIONS:
1. Extract the printed text exactly as it appears in the image.
2. DO NOT format regular English sentences, question labels, part numbers, or plain text numbers in LaTeX math mode.
3. DO NOT output LaTeX delimiters \( ... \), \[ ... \], or dollar signs $ unless an explicit mathematical formula or scientific calculation appears in the image.
4. Preserve original line breaks, part labels (e.g., '(a)', '(b)', '1.1'), and formatting.
5. PRESERVE mark allocations in square brackets like [2] or [3] if present in front of sub-parts (e.g. '[2] (a)...'), as these indicate part mark allocations for rubric generation. EXCLUDE teacher handwritten red grading marks or checkmarks.
6. DO NOT output literal \t or tab escape sequences.

Respond ONLY with a raw JSON object in the exact format below. Do not include markdown code blocks or any text outside the JSON.
{
  ""questionText"": ""The COMPLETE reference correct answer text extracted from the image, preserving original text without unnecessary LaTeX math delimiters, separated by newlines"",
  ""optionA"": """",
  ""optionB"": """",
  ""optionC"": """",
  ""optionD"": """",
  ""correctOption"": """"
}"
                    : @"You are an expert OCR transcription engine for past exam paper questions.
SUBJECT CONTEXT: Non-Quantitative Subject (" + (subjectName ?? "General Text") + @").

CRITICAL TEXT TRANSCRIPTION INSTRUCTIONS:
1. Extract the printed question text completely as it appears in the image, ensuring ALL sub-parts (e.g., '(a)', '(b)', '(c)', '1.1') are preserved.
2. DO NOT format regular English sentences, question labels, part numbers, or plain text numbers in LaTeX math mode.
3. DO NOT output LaTeX delimiters \( ... \), \[ ... \], or dollar signs $ unless an explicit mathematical formula or scientific calculation appears in the image.
4. Preserve original line breaks, part labels (e.g., '(a)', '(b)', '1.1'), and section headings.
5. EXCLUDE only empty blank student answer lines or dotted writing spaces. DO NOT omit any printed question content or sub-parts.
6. DO NOT output literal \t or tab escape sequences.

Respond ONLY with a raw JSON object in the exact format below. Do not include markdown code blocks or any text outside the JSON.
{
  ""questionText"": ""The COMPLETE question text extracted from the image with ALL sub-parts, preserving original text without unnecessary LaTeX math delimiters, separated by newlines"",
  ""optionA"": ""Option A text if present, or empty string"",
  ""optionB"": ""Option B text if present, or empty string"",
  ""optionC"": ""Option C text if present, or empty string"",
  ""optionD"": ""Option D text if present, or empty string"",
  ""correctOption"": ""The letter (A, B, C, or D) if marked, or empty string""
}";
            }
            else
            {
                return isAnswerScan
                    ? @"You are an expert OCR and mathematical transcription engine for past exam paper reference correct answers. Application rendering is powered by MathJax v3.

CRITICAL MATHEMATICAL NOTATION & LATEX INSTRUCTIONS:
1. Convert ALL mathematical expressions, equations, symbols, fractions, powers, roots, variables, matrices, formulas, AND numerical answers with subscripts/superscripts (such as numbers in base 2, base 5, base 8, or base 10 e.g. 11 001_2, 442_5, 577_8) into standard MathJax LaTeX math notation.
2. YOU MUST wrap EVERY single mathematical answer, number with subscript or base notation, algebraic expression, calculation step, and variable inside inline LaTeX delimiters \( ... \) (e.g. \( 11 001_2 \), \( 442_5 \), \( 577_8 \), \( 2x^2 + 5x - 3 = 0 \), \( \frac{3}{4} \), \( \sqrt{x} \), \( 3.5 \times 10^4 \), \( 45^\circ \)). NEVER output plain text math notation or unescaped underscores/subscripts without wrapping them in \( ... \).
3. Use block LaTeX delimiters \[ ... \] for standalone displayed equations or matrices.
4. Formatting rules:
   - Number bases & Subscripts: Wrap in inline math e.g. \( 11 001_2 \), \( 442_5 \), \( x_1 \).
   - Fractions: Use \frac{numerator}{denominator} (e.g. \( \frac{3}{4} \)).
   - Powers/Exponents: Use ^ (e.g. \( x^2 \), \( 10^{-3} \)).
   - Roots: Use \sqrt{x} or \sqrt[n]{x}.
   - Symbols: Use standard LaTeX (e.g. \theta, \pi, \alpha, \beta, \degree or ^\circ, \times, \div, \pm).
   - Trigonometry: Use \sin, \cos, \tan (e.g. \( \sin\theta \)).
   - Matrices & Vectors: Use \begin{pmatrix} a & b \\ c & d \end{pmatrix}.
   - Tables & Data Grids: Format tables using MathJax LaTeX array notation inside block math delimiters \[ \begin{array}{|c|c|} \hline Header 1 & Header 2 \\ \hline Cell 1 & Cell 2 \\ \hline \end{array} \]. Use & between columns and \\ for row line breaks.
5. IMPORTANT FOR JSON: Because your output is inside a JSON string, you MUST double-escape ALL backslashes in LaTeX expressions (e.g., use \\( and \\), \\frac, \\theta, \\sqrt, \\pm, \\begin, \\end).
6. DO NOT output literal \t, \\t, or tab escape sequences; use plain spaces for alignment.
7. DO NOT output escaped spaces like '\ ' inside math or text.
8. Ensure all number base subscripts (e.g., 11001_2, 442_5, 577_8) are formatted as _{subscript} inside \( ... \) delimiters.

CRITICAL REFERENCE ANSWER EXTRACTION INSTRUCTIONS:
1. Extract the COMPLETE reference correct answer including ALL sub-parts (a, b, c, d, etc.) and ALL numbered sections and step-by-step solutions. Do NOT stop after the first part.
2. If the reference answer has multiple parts (e.g. (a), (b), (c) or 1.1, 1.2, 1.3), you MUST include EVERY part in the questionText field as a single combined text.
3. Preserve the original part labels and hierarchy (e.g. '(a)', '(b)(i)', '1.1'), but YOU MUST wrap EVERY answer value, number in base notation (like \( 11 001_2 \)), mathematical equation, working out, steps, variables, and formulas into standard LaTeX math notation using \( ... \) and \[ ... \] delimiters. Do NOT leave raw text equations or plain text numbers with subscripts.
4. If the image contains both a question and its answer/solution, extract ONLY the reference correct answer / model solution portion.
5. PRESERVE mark allocations in square brackets like [2] or [3] if present in front of sub-parts (e.g. '[2] (a)...'), as these indicate part mark allocations for rubric generation. EXCLUDE teacher handwritten red grading marks or checkmarks.
6. Include any context, instructions, calculations, given information, or formulas that are part of the reference correct answer.
7. Separate each sub-part with a newline character in the questionText.

Respond with a JSON object in the format below:
{
  ""questionText"": ""The COMPLETE reference correct answer text with ALL parts and solution steps extracted from the image, strictly formatted with LaTeX math notation using \\( ... \\) and \\[ ... \\], separated by newlines"",
  ""optionA"": """",
  ""optionB"": """",
  ""optionC"": """",
  ""optionD"": """",
  ""correctOption"": """"
}
If there are no multiple choice options, set all option fields to empty strings.
Extract all reference correct answer content accurately without paraphrasing or adding external commentary, ensuring every mathematical expression and number base notation is wrapped in LaTeX delimiters."
                    : @"You are an expert OCR and mathematical transcription engine for past exam paper questions. Application rendering is powered by MathJax v3.

CRITICAL MATHEMATICAL NOTATION & LATEX INSTRUCTIONS:
1. Convert ALL mathematical expressions, equations, symbols, fractions, powers, roots, variables, matrices, formulas, AND numbers with subscripts/superscripts (such as numbers in base 2, base 5, base 8, or base 10 e.g. 11 001_2, 442_5, 577_8) into standard MathJax LaTeX math notation.
2. YOU MUST wrap EVERY single mathematical expression, number with subscript or base notation, equation, symbol, and variable inside inline LaTeX delimiters \( ... \) (e.g. \( 11 001_2 \), \( 442_5 \), \( 577_8 \), \( 2x^2 + 5x - 3 = 0 \), \( \frac{3}{4} \), \( \sqrt{x} \), \( 3.5 \times 10^4 \), \( 45^\circ \)). NEVER output plain text math notation or unescaped underscores/subscripts without wrapping them in \( ... \).
3. Use block LaTeX delimiters \[ ... \] for standalone displayed equations or matrices.
4. Formatting rules:
   - Number bases & Subscripts: Wrap in inline math e.g. \( 11 001_2 \), \( 442_5 \), \( x_1 \).
   - Fractions: Use \frac{numerator}{denominator} (e.g. \( \frac{3}{4} \)).
   - Powers/Exponents: Use ^ (e.g. \( x^2 \), \( 10^{-3} \)).
   - Roots: Use \sqrt{x} or \sqrt[n]{x}.
   - Symbols: Use standard LaTeX (e.g. \theta, \pi, \alpha, \beta, \degree or ^\circ, \times, \div, \pm).
   - Trigonometry: Use \sin, \cos, \tan (e.g. \( \sin\theta \)).
   - Matrices & Vectors: Use \begin{pmatrix} a & b \\ c & d \end{pmatrix}.
   - Tables & Data Grids: Format tables using MathJax LaTeX array notation inside block math delimiters \[ \begin{array}{|c|c|} \hline Header 1 & Header 2 \\ \hline Cell 1 & Cell 2 \\ \hline \end{array} \]. Use & between columns and \\ for row line breaks.
5. IMPORTANT FOR JSON: Because your output is inside a JSON string, you MUST double-escape ALL backslashes in LaTeX expressions (e.g., use \\( and \\), \\frac, \\theta, \\sqrt, \\pm, \\begin, \\end).
6. DO NOT output literal \t, \\t, or tab escape sequences; use plain spaces for alignment.
7. DO NOT output escaped spaces like '\ ' inside math or text.
8. Ensure all number base subscripts (e.g., 11001_2, 442_5, 577_8) are formatted as _{subscript} inside \( ... \) delimiters.

CRITICAL QUESTION EXTRACTION INSTRUCTIONS:
1. Extract the COMPLETE question including ALL sub-parts (a, b, c, d, etc.) and ALL numbered sections. Do NOT stop after the first part.
2. If the question has multiple parts (e.g. (a), (b), (c) or 1.1, 1.2, 1.3), you MUST include EVERY part in the questionText field as a single combined text.
3. Preserve the original part labels and hierarchy (e.g. '(a)', '(b)', '1.1'), but YOU MUST convert ALL mathematical equations, number base notation, variables, and formulas into standard LaTeX math notation using \( ... \) and \[ ... \] delimiters. Do NOT leave raw text equations or plain text numbers with subscripts.
4. EXCLUDE only empty student answer blank lines, answer response boxes, or dotted writing spaces. DO NOT omit any printed question content, diagram text, or sub-questions.
5. PRESERVE mark allocations in square brackets like [2] if present, or omit them only if they appear on separate empty lines. NEVER skip sub-questions because of mark allocations.
6. Include any context, instructions, diagram descriptions, given information, or formulas that are part of the question.
7. Separate each sub-part with a newline character in the questionText.

Respond with a JSON object in the format below:
{
  ""questionText"": ""The COMPLETE question text with ALL parts extracted from the image, strictly formatted with LaTeX math notation using \\( ... \\) and \\[ ... \\], separated by newlines"",
  ""optionA"": ""Option A text with LaTeX math notation if present, or empty string"",
  ""optionB"": ""Option B text with LaTeX math notation if present, or empty string"",
  ""optionC"": ""Option C text with LaTeX math notation if present, or empty string"",
  ""optionD"": ""Option D text with LaTeX math notation if present, or empty string"",
  ""correctOption"": ""The letter (A, B, C, or D) if the correct answer is indicated in the image, otherwise empty string""
}
If there are no multiple choice options, set all option fields to empty strings.
Extract all question content accurately without paraphrasing or adding external commentary, ensuring every mathematical expression and number base notation is wrapped in LaTeX delimiters.";
            }
        }

        public (string Prompt, string VisionPrompt, List<string> Images, Dictionary<string, int> BracketMap, int TotalBracketPoints) BuildRubricPrompt(GenerateRubricRequestDto request)
        {
            bool hasUserPoints = request.Points > 0;

            var (bracketMap, totalBracketPoints) = ExtractBracketPoints(request.ReferenceAnswer);
            if (totalBracketPoints == 0)
            {
                var (qBracketMap, qTotalBracketPoints) = ExtractBracketPoints(request.QuestionText);
                if (qTotalBracketPoints > 0)
                {
                    bracketMap = qBracketMap;
                    totalBracketPoints = qTotalBracketPoints;
                }
            }

            if (totalBracketPoints > 0 && (!hasUserPoints || request.Points == 1))
            {
                request.Points = totalBracketPoints;
                hasUserPoints = true;
            }

            var bracketInstruction = totalBracketPoints > 0
                ? $"CRITICAL EXPLICIT MARK ALLOCATION: The user/teacher provided bracketed points [N] in front of reference answer parts (Total: {totalBracketPoints} points). You MUST assign EXACTLY N max points to the criterion/criteria for each corresponding part (e.g., if part (a) is marked [2], part (a)'s criteria MUST sum to 2 points; if part (b) is marked [3], part (b)'s criteria MUST sum to 3 points)."
                : "";

            var pointConstraintInstructions = hasUserPoints
                ? $"CRITICAL: The required total marks for this question is {request.Points} point(s). The sum of MaxPoints across ALL generated criteria MUST EXACTLY EQUAL {request.Points}. {bracketInstruction}"
                : @"CONSERVATIVE MARKING RULES (When total points are not specified):
- Be conservative with mark allocations! A single standalone question or any single sub-part (e.g. (a), (b), (c)) MUST range from 1 to 3 points MAXIMUM depending on complexity:
  * 1 Point: Simple one-step recall, single calculation result, or basic statement.
  * 2 Points: 2-step working/substitution, or definition with 2 distinct concepts.
  * 3 Points: Complex multi-step calculation or detailed multi-factor explanation.
- NEVER assign more than 3 points to any single sub-part.
- Keep the overall total conservative (e.g., a 2-part question should total 2 to 4 points; a 3-part question should total 3 to 6 points max).";

            var prompt = $@"# Role and Task
You are an expert ZIMSEC (Zimbabwe School Examinations Council) Form 4 / O-Level marking scheme designer.
Your task is to generate a precise, conservative, structured grading rubric for an exam question by analyzing the Question Text and Reference Correct Answer.

# ZIMSEC Marking Conventions You MUST Follow
1. **Mark Allocation Rules**:
   {pointConstraintInstructions}
2. **Explicit Bracketed Part Points `[N]`**:
   - If the Reference Correct Answer or Question text contains points in square brackets `[N]` (such as `[2] (a)...`, `(b) [3]...`, `[1] (i)...`), this indicates the EXACT max points allocated by the teacher for that specific part.
   - The generated criteria for that part MUST have `MaxPoints` summing to EXACTLY N.
3. **1 Mark per Distinct Scorable Element**: Award 1 mark per key step, fact, or formula. Method marks (M1) are awarded even if the final answer is wrong.
4. **Multi-Part Questions**: If the question has sub-parts (e.g. (a), (b), (c), (i), (ii)), create SEPARATE criteria for EACH sub-part. Label criteria clearly (e.g. ""Part (a)"", ""Part (b)"").
5. **Calculation / Math Questions**: Break down logically into: formula/method (1 mark), substitution/working (1 mark), final answer with units (1 mark). Maximum 3 points per sub-part (unless explicitly specified higher in brackets [N]).
6. **Definition / Explanation Questions**: 1 mark per key concept mentioned. Maximum 3 points per sub-part (unless explicitly specified higher in brackets [N]).

# Inputs
- Question: {request.QuestionText}
- Reference Correct Answer: {request.ReferenceAnswer}
{(hasUserPoints ? $"- Required Exact Total Points: {request.Points}" : "")}

# Few-Shot ZIMSEC-Style Examples

## Example 1: Multi-part Maths Question (Total: 3 Points)
Question: ""(a) Simplify 3x + 2x - 4\n(b) Solve for x: 2x + 5 = 13""
Reference Answer: ""(a) 5x - 4\n(b) 2x = 13 - 5, 2x = 8, x = 4""
[
  {{""Criterion"":""Part (a) — Simplification"",""MaxPoints"":1,""Description"":""1 mark: Correctly simplifies to 5x - 4.""}},
  {{""Criterion"":""Part (b) — Method"",""MaxPoints"":1,""Description"":""1 mark: Correctly rearranges to 2x = 8.""}},
  {{""Criterion"":""Part (b) — Final Answer"",""MaxPoints"":1,""Description"":""1 mark: Correctly solves x = 4.""}}
]

## Example 2: Science Question (Total: 4 Points)
Question: ""(a) Define osmosis.\n(b) Explain why a plant cell in salt solution becomes plasmolysed.""
Reference Answer: ""(a) Osmosis is the movement of water molecules from higher to lower water concentration through a semi-permeable membrane.\n(b) Salt solution has lower water concentration so water moves out by osmosis causing cell membrane to pull away from cell wall.""
[
  {{""Criterion"":""Part (a) — Define osmosis"",""MaxPoints"":2,""Description"":""1 mark: Movement of water molecules from higher to lower water concentration. 1 mark: Through semi-permeable membrane.""}},
  {{""Criterion"":""Part (b) — Plasmolysis"",""MaxPoints"":2,""Description"":""1 mark: Identifies water moves out by osmosis due to concentration gradient. 1 mark: Explains cell membrane shrinks away from cell wall.""}}
]

## Example 3: Reference Answer with Explicit Bracketed Part Points (e.g. [2], [3])
Question: ""(a) Define velocity.\n(b) A car accelerates from rest to 20 m/s in 5 s. Calculate acceleration.""
Reference Answer: ""[1] (a) Velocity is rate of change of displacement.\n[3] (b) a = (v - u)/t = (20 - 0)/5 = 4 m/s^2""
[
  {{""Criterion"":""Part (a) — Define velocity"",""MaxPoints"":1,""Description"":""1 mark: Defines as rate of change of displacement.""}},
  {{""Criterion"":""Part (b) — Formula & Working"",""MaxPoints"":2,""Description"":""1 mark: Correct formula and substitution (20 - 0)/5. 1 mark: Correct evaluation.""}},
  {{""Criterion"":""Part (b) — Final Answer & Units"",""MaxPoints"":1,""Description"":""1 mark: Correct answer 4 m/s^2 with units.""}}
]

# Output Rules
1. {(hasUserPoints ? $"The sum of MaxPoints MUST EXACTLY EQUAL {request.Points}." : "Keep point allocations conservative: 1 to 3 points max per sub-part.")}
2. For multi-part questions, create separate criteria per sub-part with part labels in Criterion names.
3. For each criterion, state clearly in Description what earns each mark.
4. Use LaTeX formatting \\( ... \\) for math expressions.
5. Respond with a JSON array formatting each criterion cleanly.

Format:
[
  {{
    ""Criterion"": ""Criterion Name"",
    ""MaxPoints"": integer,
    ""Description"": ""Detailed mark-by-mark breakdown""
  }}
]";

            var images = new List<string>();
            if (!string.IsNullOrWhiteSpace(request.QuestionImageUrl))
            {
                images.Add(request.QuestionImageUrl.Contains(",") ? request.QuestionImageUrl.Split(',')[1] : request.QuestionImageUrl);
            }
            if (!string.IsNullOrWhiteSpace(request.ReferenceAnswerImageUrl))
            {
                images.Add(request.ReferenceAnswerImageUrl.Contains(",") ? request.ReferenceAnswerImageUrl.Split(',')[1] : request.ReferenceAnswerImageUrl);
            }

            var visionRubricPrompt = $@"You are an expert marking scheme designer.
Generate a structured grading rubric for an exam question based on the question text, reference correct answer, and attached diagram image(s).

Question: {request.QuestionText}
Reference Correct Answer: {request.ReferenceAnswer}
{(hasUserPoints ? $"Total Points: {request.Points}" : "")}
{(totalBracketPoints > 0 ? $"Explicit Part Mark Allocations: Total {totalBracketPoints} points. Ensure criteria match part allocations e.g. [N]." : "")}

Rules:
1. {(hasUserPoints ? $"The sum of MaxPoints across all criteria MUST EQUAL {request.Points}." : "Keep point allocations conservative (1 to 3 points per part).")}
2. For multi-part questions (e.g. (a), (b)), create separate criteria for each sub-part with part labels in Criterion names.
3. Respond ONLY with a valid JSON array of criteria.

Format:
[
  {{
    ""Criterion"": ""Part (a) — Description"",
    ""MaxPoints"": integer,
    ""Description"": ""Detailed mark breakdown""
  }}
]";

            return (prompt, visionRubricPrompt, images, bracketMap, totalBracketPoints);
        }

        public bool IsQuantitativeSubject(string? subjectName, bool? isQuantitative)
        {
            if (isQuantitative.HasValue) return isQuantitative.Value;
            if (string.IsNullOrWhiteSpace(subjectName)) return true;

            var s = subjectName.Trim().ToLowerInvariant();

            string[] nonQuantKeywords = new[]
            {
                "english", "history", "geography", "literature", "shona", "ndebele",
                "divinity", "religious", "commerce", "business", "law", "social",
                "sociology", "heritage", "civic", "cre", "ire"
            };

            foreach (var kw in nonQuantKeywords)
            {
                if (s.Contains(kw)) return false;
            }

            return true;
        }

        private static (Dictionary<string, int> partPoints, int totalBracketPoints) ExtractBracketPoints(string text)
        {
            var partPoints = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(text)) return (partPoints, 0);

            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int totalPoints = 0;

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var match = Regex.Match(line, @"(?:\[(\d+)\]\s*(?:\(?([a-zA-Z0-9\.]+)\)?)?|(?:\(?([a-zA-Z0-9\.]+)\)?)\s*\[(\d+)\])", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    int pts = 0;
                    string label = "";

                    if (match.Groups[1].Success && int.TryParse(match.Groups[1].Value, out pts))
                    {
                        label = match.Groups[2].Value;
                    }
                    else if (match.Groups[4].Success && int.TryParse(match.Groups[4].Value, out pts))
                    {
                        label = match.Groups[3].Value;
                    }

                    if (pts > 0)
                    {
                        totalPoints += pts;
                        if (!string.IsNullOrWhiteSpace(label))
                        {
                            var cleanLabel = label.Trim('(', ')', '.', ' ').ToLowerInvariant();
                            if (!string.IsNullOrWhiteSpace(cleanLabel))
                            {
                                partPoints[cleanLabel] = pts;
                            }
                        }
                    }
                }
            }

            return (partPoints, totalPoints);
        }

        private string GetCoreGradingPhilosophyPrompt(QuizQuestion question, bool isHandwrittenPhoto = false)
        {
            var handwrittenNote = isHandwrittenPhoto
                ? "The student has submitted a photo of their handwritten solution. Read and interpret the student's handwritten answer from the attached image.\n\n"
                : "";

            return $@"# Role and Objective
You are an expert, unbiased K-12 AI Grader. Your task is to evaluate a student's response against a provided Question, Correct Answer/Criteria, and Rubric. You must provide fair, consistent, and accurate scoring.
{handwrittenNote}# Core Grading Philosophy
1. Equifinality (Multiple Pathways): Accept alternative methods, wording, or problem-solving steps if they are mathematically, logically, or scientifically sound and align with the rubric's core criteria. Do not penalize a student for using a method different from the answer key unless the prompt explicitly required a specific method.
2. Fact-Checking & Accuracy: Never award marks for incorrect final answers or fundamentally flawed conceptual logic, even if the writing is fluent or uses correct keywords.
3. Strict Evidence-Based Scoring: Award points strictly based on explicit evidence in the student's text. Do not make assumptions or ""read between the lines"" to grant unearned points.

# Question-Specific Guidelines

## 1. Quantitative & Math Questions
- Process vs. Product: Check the final answer first. If correct, verify the steps. If incorrect, evaluate the intermediate steps for partial credit according to the rubric.
- Strategic Flexibility: Accept alternative algebraic manipulations, visual modeling strategies, or arithmetic setups, provided they are mathematically rigorous.

## 2. Short-Answer Questions
- Synonyms and Phrasing: Grade based on conceptual understanding, not exact keyword matching. Accept valid synonyms and varied sentence structures.
- Intent vs. Content: The student must clearly demonstrate the target knowledge. If the response is ambiguous or partially incorrect, only award points for the demonstrably correct portion.

## 3. Essay & Extended Responses
- Rubric Alignment: Evaluate the essay dimension by dimension (e.g., Content, Organization, Mechanics) as isolated metrics. Do not let poor spelling bias the score for content unless mechanics are explicitly part of the rubric.
- Argumentation: Value the logical flow and use of evidence over whether the student matches a specific viewpoint or pre-penned model essay.

## Strict Final Answer Guardrail (Mandatory)
- Calculation vs. Transcription: You must explicitly calculate the student's final line yourself. Do not assume it is correct because the previous steps are correct.
- Zero-Tolerance for Incorrect Finals: If the final numeric answer or final conclusion is factually or mathematically incorrect, you are strictly FORBIDDEN from awarding full marks, regardless of how perfect the preceding steps are. 
- Partial Credit Enforcement: If the student did everything right but made a calculation or transcription error at the very end, you must deduct the specific point(s) allocated by the rubric for the final answer. If the rubric does not specify a breakdown, automatically deduct a minimum of 10% to 20% of the total question value for the incorrect final answer. You must document this deduction explicitly in your breakdown.

### Feedback
*   **Strengths**: [What the student did well].
*   **Areas for Improvement**: [Constructive advice pointing out the specific error or missing element, without giving away the direct answer if it is a multi-part quiz].

Question: {question.QuestionText}
Max Points: {question.Points}
Reference Correct Answer: {question.CorrectAnswer ?? "N/A"}
Rubric (JSON): {question.RubricJson ?? "[]"}

OUTPUT FORMAT:
Output your response ONLY as a raw JSON object in the exact format:
{{
  ""PointsAwarded"": integer,
  ""CriteriaBreakdown"": ""string"",
  ""Feedback"": ""string""
}}

The ""CriteriaBreakdown"" string MUST contain exactly the following structure:
### Breakdown by Criteria
*   [Criterion Name 1]: [Score] - [1-2 sentences justifying the score based strictly on student evidence].
*   [Criterion Name 2]: [Score] - [1-2 sentences justifying the score based strictly on student evidence].

The ""Feedback"" string MUST contain exactly the following structure:
**Total Score**: [Earned Points] / [Total Possible Points]

### Feedback
*   **Strengths**: [What the student did well].
*   **Areas for Improvement**: [Constructive advice pointing out the specific error or missing element, without giving away the direct answer if it is a multi-part quiz].

Do not output markdown code blocks (e.g. ```json). Output just the JSON string.";
        }
    }
}
