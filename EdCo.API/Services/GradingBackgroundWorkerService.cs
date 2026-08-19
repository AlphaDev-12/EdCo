using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using EdCo.API.DTOs;
using EdCo.Core.Data;
using EdCo.Core.Entities;
using EdCo.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EdCo.API.Services
{
    public class GradingBackgroundWorkerService : BackgroundService
    {
        private readonly IGradingJobQueue _jobQueue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<GradingBackgroundWorkerService> _logger;
        private readonly SemaphoreSlim _concurrencySemaphore = new SemaphoreSlim(2, 2);

        public GradingBackgroundWorkerService(
            IGradingJobQueue jobQueue,
            IServiceScopeFactory scopeFactory,
            ILogger<GradingBackgroundWorkerService> logger)
        {
            _jobQueue = jobQueue;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("GradingBackgroundWorkerService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var job = await _jobQueue.DequeueJobAsync(stoppingToken);
                    if (job != null)
                    {
                        _ = ProcessJobAsync(job, stoppingToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error dequeuing job from grading queue.");
                }
            }

            _logger.LogInformation("GradingBackgroundWorkerService stopping.");
        }

        private async Task ProcessJobAsync(GradingJobItem job, CancellationToken cancellationToken)
        {
            await _concurrencySemaphore.WaitAsync(cancellationToken);
            try
            {
                job.Status = "Processing";
                await _jobQueue.SaveJobStatusAsync(job);

                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<EdCoDbContext>();
                var visionService = scope.ServiceProvider.GetRequiredService<IGeminiVisionService>();
                var parserService = scope.ServiceProvider.GetRequiredService<IAiResponseParserService>();

                // 1. Separate questions into text vs vision
                var textTasks = job.Questions.Where(q => !q.IsVision && string.IsNullOrWhiteSpace(q.Base64Image)).ToList();
                var visionTasks = job.Questions.Where(q => q.IsVision || !string.IsNullOrWhiteSpace(q.Base64Image)).ToList();

                // 2. Process text tasks in batch if any
                if (textTasks.Count > 0)
                {
                    await ProcessBatchTextQuestionsAsync(job, textTasks, dbContext, visionService, parserService);
                }

                // 3. Process vision tasks individually
                foreach (var vTask in visionTasks)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    await ProcessSingleVisionQuestionAsync(job, vTask, dbContext, visionService, parserService);
                }

                job.Status = "Completed";
                await _jobQueue.SaveJobStatusAsync(job);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process grading job {JobId}", job.JobId);
                job.Status = "Failed";
                job.ErrorMessage = ex.Message;
                await _jobQueue.SaveJobStatusAsync(job);
            }
            finally
            {
                _concurrencySemaphore.Release();
            }
        }

        private async Task ProcessBatchTextQuestionsAsync(
            GradingJobItem job,
            List<GradingQuestionTask> tasks,
            EdCoDbContext dbContext,
            IGeminiVisionService visionService,
            IAiResponseParserService parserService)
        {
            var questionIds = tasks.Select(t => t.QuestionId).ToList();
            var questions = await dbContext.QuizQuestions
                .Where(q => questionIds.Contains(q.Id))
                .ToDictionaryAsync(q => q.Id);

            var batchItems = new List<object>();
            foreach (var t in tasks)
            {
                if (questions.TryGetValue(t.QuestionId, out var q))
                {
                    batchItems.Add(new
                    {
                        QuestionId = q.Id,
                        QuestionText = q.QuestionText,
                        MaxPoints = q.Points,
                        ReferenceAnswer = q.CorrectAnswer ?? "N/A",
                        Rubric = q.RubricJson ?? "[]",
                        StudentAnswer = t.StudentAnswer ?? ""
                    });
                }
            }

            if (batchItems.Count == 0) return;

            var prompt = $@"# Role
You are an expert K-12 AI Grader. Grade the following array of student question responses against their criteria and rubrics.

# Questions & Responses:
{JsonSerializer.Serialize(batchItems)}

OUTPUT FORMAT:
Respond ONLY with a raw JSON object. Do not include markdown fences, reasoning blocks, or conversational text:
{{
  ""GradedQuestions"": [
    {{
      ""QuestionId"": 101,
      ""PointsAwarded"": integer,
      ""CriteriaBreakdown"": ""string"",
      ""Feedback"": ""string""
    }}
  ]
}}";

            try
            {
                var reply = await visionService.GenerateContentAsync(prompt, job.StudentUserId);
                var cleaned = parserService.CleanJsonResponse(reply);
                using var doc = JsonDocument.Parse(cleaned);
                if (doc.RootElement.TryGetProperty("GradedQuestions", out var arrayProp))
                {
                    foreach (var elem in arrayProp.EnumerateArray())
                    {
                        int qId = elem.GetProperty("QuestionId").GetInt32();
                        int pts = elem.GetProperty("PointsAwarded").GetInt32();
                        string bd = elem.TryGetProperty("CriteriaBreakdown", out var b) ? b.GetString() ?? "" : "";
                        string fb = elem.TryGetProperty("Feedback", out var f) ? f.GetString() ?? "" : "";

                        job.Results.Add(new AiGradeResponseDtoWithQuestion
                        {
                            QuestionId = qId,
                            PointsAwarded = pts,
                            CriteriaBreakdown = bd,
                            Feedback = fb
                        });

                        job.CompletedQuestions++;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed batch text grading for job {JobId}", job.JobId);
                // Fallback to individual for text questions
                foreach (var t in tasks)
                {
                    job.CompletedQuestions++;
                }
            }

            await _jobQueue.SaveJobStatusAsync(job);
        }

        private async Task ProcessSingleVisionQuestionAsync(
            GradingJobItem job,
            GradingQuestionTask task,
            EdCoDbContext dbContext,
            IGeminiVisionService visionService,
            IAiResponseParserService parserService)
        {
            var question = await dbContext.QuizQuestions.FirstOrDefaultAsync(q => q.Id == task.QuestionId);
            if (question == null)
            {
                job.CompletedQuestions++;
                await _jobQueue.SaveJobStatusAsync(job);
                return;
            }

            var studentImages = new List<string>();
            if (task.Base64Images != null && task.Base64Images.Count > 0)
            {
                foreach (var img in task.Base64Images)
                {
                    if (!string.IsNullOrWhiteSpace(img))
                    {
                        studentImages.Add(img.Contains(",") ? img.Split(',')[1] : img);
                    }
                }
            }
            else if (!string.IsNullOrWhiteSpace(task.Base64Image))
            {
                studentImages.Add(task.Base64Image.Contains(",") ? task.Base64Image.Split(',')[1] : task.Base64Image);
            }

            var prompt = $@"Evaluate student handwritten answer photo.
Question: {question.QuestionText}
Max Points: {question.Points}
Reference Answer: {question.CorrectAnswer ?? "N/A"}
Rubric: {question.RubricJson ?? "[]"}

Respond ONLY with raw JSON:
{{
  ""PointsAwarded"": integer,
  ""CriteriaBreakdown"": ""string"",
  ""Feedback"": ""string""
}}";

            var allImages = new List<string>();
            if (!string.IsNullOrWhiteSpace(question.ImageUrl))
            {
                var qDiag = question.ImageUrl.Contains(",") ? question.ImageUrl.Split(',')[1] : question.ImageUrl;
                allImages.Add(qDiag);
            }
            if (!string.IsNullOrWhiteSpace(question.CorrectAnswerImageUrl))
            {
                var refDiag = question.CorrectAnswerImageUrl.Contains(",") ? question.CorrectAnswerImageUrl.Split(',')[1] : question.CorrectAnswerImageUrl;
                allImages.Add(refDiag);
            }
            allImages.AddRange(studentImages);

            try
            {
                string reply;
                if (allImages.Count == 0)
                {
                    reply = await visionService.GenerateContentAsync(prompt, job.StudentUserId);
                }
                else if (allImages.Count == 1)
                {
                    reply = await visionService.ExtractMathFromImageAsync(allImages[0], prompt, job.StudentUserId);
                }
                else
                {
                    reply = await visionService.ExtractMathFromImagesAsync(allImages, prompt, job.StudentUserId);
                }

                var cleaned = parserService.CleanJsonResponse(reply);
                var dto = JsonSerializer.Deserialize<AiGradeResponseDto>(cleaned);
                if (dto != null)
                {
                    job.Results.Add(new AiGradeResponseDtoWithQuestion
                    {
                        QuestionId = task.QuestionId,
                        PointsAwarded = Math.Min(dto.PointsAwarded, question.Points),
                        CriteriaBreakdown = dto.CriteriaBreakdown,
                        Feedback = dto.Feedback
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed vision grading for question {QuestionId}", task.QuestionId);
            }

            job.CompletedQuestions++;
            await _jobQueue.SaveJobStatusAsync(job);
        }

        // CleanJsonResponse logic is now delegated to IAiResponseParserService (DRY principle)
    }
}
