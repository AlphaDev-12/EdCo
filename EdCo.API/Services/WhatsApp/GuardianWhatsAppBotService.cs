using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using EdCo.Core.Data;
using EdCo.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EdCo.API.Services.WhatsApp
{
    public interface IGuardianWhatsAppBotService
    {
        Task ProcessIncomingMessageAsync(string phoneNumber, string messageType, string payload, string? interactiveId = null);
    }

    /// <summary>
    /// State-machine bot engine for guardian WhatsApp interactions.
    /// Supports: Student linking (via email+password), performance viewing, Ecocash payments.
    /// Adapted from EduPay's WhatsAppBotService.
    /// </summary>
    public class GuardianWhatsAppBotService : IGuardianWhatsAppBotService
    {
        private readonly IWhatsAppService _whatsAppService;
        private readonly EdCoDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly IEcocashService _ecocashService;
        private readonly ILogger<GuardianWhatsAppBotService> _logger;

        public GuardianWhatsAppBotService(
            IWhatsAppService whatsAppService,
            EdCoDbContext context,
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            IEcocashService ecocashService,
            ILogger<GuardianWhatsAppBotService> logger)
        {
            _whatsAppService = whatsAppService;
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _ecocashService = ecocashService;
            _logger = logger;
        }

        public async Task ProcessIncomingMessageAsync(string phoneNumber, string messageType, string payload, string? interactiveId = null)
        {
            try
            {
                var session = await GetOrCreateSessionAsync(phoneNumber);
                var textContent = messageType == "text" ? payload : (interactiveId ?? "");
                var action = textContent.Trim().ToLower();

                // Check if guardian has any linked students
                var hasLinkedStudents = await _context.GuardianLinks
                    .AnyAsync(gl => gl.PhoneNumber == phoneNumber);

                // Global commands to reset state
                if (action == "menu" || action == "home" || action == "hi" || action == "hello")
                {
                    if (hasLinkedStudents)
                    {
                        session.CurrentState = "MainMenu";
                        await _context.SaveChangesAsync();
                        await SendMainMenuAsync(phoneNumber);
                    }
                    else
                    {
                        session.CurrentState = "LinkEmail";
                        session.ContextData = null;
                        await _context.SaveChangesAsync();
                        await _whatsAppService.SendTextMessageAsync(phoneNumber,
                            "🎓 *Welcome to the EdCo Guardian Portal!*\n\n" +
                            "To get started, you'll need to link your account to your child's student profile.\n\n" +
                            "Please reply with your child's *student email address* that they use to log in to EdCo.");
                    }
                    return;
                }

                // State Machine Dispatch
                switch (session.CurrentState)
                {
                    case "Initial":
                        if (hasLinkedStudents)
                        {
                            session.CurrentState = "MainMenu";
                            await _context.SaveChangesAsync();
                            await SendMainMenuAsync(phoneNumber);
                        }
                        else
                        {
                            session.CurrentState = "LinkEmail";
                            session.ContextData = null;
                            await _context.SaveChangesAsync();
                            await _whatsAppService.SendTextMessageAsync(phoneNumber,
                                "🎓 *Welcome to the EdCo Guardian Portal!*\n\n" +
                                "To get started, you'll need to link your account to your child's student profile.\n\n" +
                                "Please reply with your child's *student email address* that they use to log in to EdCo.");
                        }
                        break;

                    case "LinkEmail":
                        await HandleLinkEmailAsync(phoneNumber, session, textContent.Trim());
                        break;

                    case "LinkPassword":
                        await HandleLinkPasswordAsync(phoneNumber, session, textContent.Trim());
                        break;

                    case "MainMenu":
                        await HandleMainMenuActionAsync(phoneNumber, session, action);
                        break;

                    case "SelectStudent":
                        await HandleStudentSelectionAsync(phoneNumber, session, action);
                        break;

                    case "ViewPerformance":
                        // Performance is displayed immediately, this state shouldn't persist
                        session.CurrentState = "MainMenu";
                        await _context.SaveChangesAsync();
                        await SendMainMenuAsync(phoneNumber);
                        break;

                    case "PaySubscription":
                        await HandlePaymentConfirmationAsync(phoneNumber, session, action);
                        break;

                    case "LinkStudent":
                        // Guardian wants to link another student — start with email
                        session.CurrentState = "LinkEmail";
                        session.ContextData = null;
                        await _context.SaveChangesAsync();
                        await _whatsAppService.SendTextMessageAsync(phoneNumber,
                            "Please reply with the student's *email address* that they use to log in to EdCo.");
                        break;

                    default:
                        session.CurrentState = hasLinkedStudents ? "MainMenu" : "Initial";
                        await _context.SaveChangesAsync();
                        if (hasLinkedStudents)
                            await SendMainMenuAsync(phoneNumber);
                        else
                            await _whatsAppService.SendTextMessageAsync(phoneNumber,
                                "Welcome! Type *hi* to get started.");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing incoming WhatsApp message from {PhoneNumber}", phoneNumber);
            }
        }

        // ───────────────────────────── Session Management ─────────────────────────────

        private async Task<WhatsAppSession> GetOrCreateSessionAsync(string phoneNumber)
        {
            var session = await _context.WhatsAppSessions.FirstOrDefaultAsync(s => s.PhoneNumber == phoneNumber);
            if (session == null)
            {
                session = new WhatsAppSession
                {
                    PhoneNumber = phoneNumber,
                    CurrentState = "Initial",
                    LastActiveAt = DateTime.UtcNow
                };
                _context.WhatsAppSessions.Add(session);
            }
            else
            {
                session.LastActiveAt = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync();
            return session;
        }

        // ───────────────────────────── Main Menu ─────────────────────────────

        private async Task SendMainMenuAsync(string phoneNumber)
        {
            var sections = new List<InteractiveListSection>
            {
                new InteractiveListSection
                {
                    Title = "Guardian Options",
                    Rows = new List<InteractiveListRow>
                    {
                        new InteractiveListRow { Id = "menu_performance", Title = "📊 View Performance", Description = "See your child's quiz scores" },
                        new InteractiveListRow { Id = "menu_pay", Title = "💰 Pay Subscription", Description = "Pay via Ecocash" },
                        new InteractiveListRow { Id = "menu_link", Title = "🔗 Link Student", Description = "Add another child" }
                    }
                }
            };

            await _whatsAppService.SendInteractiveListAsync(
                phoneNumber,
                "🎓 *EdCo Guardian Portal*\n\nHow can we help you today?",
                "Select Option",
                sections);
        }

        private async Task HandleMainMenuActionAsync(string phoneNumber, WhatsAppSession session, string action)
        {
            switch (action)
            {
                case "menu_performance":
                    await PromptStudentSelectionAsync(phoneNumber, session, "view_performance");
                    break;

                case "menu_pay":
                    await PromptStudentSelectionAsync(phoneNumber, session, "pay_subscription");
                    break;

                case "menu_link":
                    session.CurrentState = "LinkEmail";
                    session.ContextData = null;
                    await _context.SaveChangesAsync();
                    await _whatsAppService.SendTextMessageAsync(phoneNumber,
                        "Please reply with the student's *email address* that they use to log in to EdCo.");
                    break;

                default:
                    await SendMainMenuAsync(phoneNumber);
                    break;
            }
        }

        // ───────────────────────────── Student Linking (Email + Password) ─────────────────────────────

        private async Task HandleLinkEmailAsync(string phoneNumber, WhatsAppSession session, string email)
        {
            // Validate basic email format
            if (string.IsNullOrWhiteSpace(email) || !email.Contains("@") || !email.Contains("."))
            {
                await _whatsAppService.SendTextMessageAsync(phoneNumber,
                    "❌ That doesn't look like a valid email address. Please enter the student's email address.");
                return;
            }

            // Check if user exists
            var student = await _userManager.FindByEmailAsync(email);
            if (student == null)
            {
                await _whatsAppService.SendTextMessageAsync(phoneNumber,
                    "❌ No student account found with that email. Please check and try again, or type *menu* to go back.");
                return;
            }

            // Check if already linked
            var alreadyLinked = await _context.GuardianLinks
                .AnyAsync(gl => gl.PhoneNumber == phoneNumber && gl.StudentUserId == student.Id);

            if (alreadyLinked)
            {
                await _whatsAppService.SendTextMessageAsync(phoneNumber,
                    $"You are already linked to *{student.FullName ?? student.Email}*. Type *menu* to go back.");
                session.CurrentState = "MainMenu";
                await _context.SaveChangesAsync();
                await SendMainMenuAsync(phoneNumber);
                return;
            }

            // Store the email for password verification
            session.CurrentState = "LinkPassword";
            session.ContextData = JsonSerializer.Serialize(new { Email = email, StudentId = student.Id });
            await _context.SaveChangesAsync();

            await _whatsAppService.SendTextMessageAsync(phoneNumber,
                $"✅ Found student: *{student.FullName ?? "Student"}*\n\n" +
                "For security, please reply with the student's *account password* to confirm the link.");
        }

        private async Task HandleLinkPasswordAsync(string phoneNumber, WhatsAppSession session, string password)
        {
            if (string.IsNullOrEmpty(session.ContextData))
            {
                session.CurrentState = "LinkEmail";
                session.ContextData = null;
                await _context.SaveChangesAsync();
                await _whatsAppService.SendTextMessageAsync(phoneNumber,
                    "Something went wrong. Please enter the student's email address to start again.");
                return;
            }

            string email;
            string studentId;
            try
            {
                using var doc = JsonDocument.Parse(session.ContextData);
                email = doc.RootElement.GetProperty("Email").GetString() ?? "";
                studentId = doc.RootElement.GetProperty("StudentId").GetString() ?? "";
            }
            catch
            {
                session.CurrentState = "LinkEmail";
                session.ContextData = null;
                await _context.SaveChangesAsync();
                await _whatsAppService.SendTextMessageAsync(phoneNumber,
                    "Something went wrong. Please enter the student's email address to start again.");
                return;
            }

            var student = await _userManager.FindByIdAsync(studentId);
            if (student == null)
            {
                await _whatsAppService.SendTextMessageAsync(phoneNumber,
                    "❌ Student account not found. Please type *menu* to start over.");
                session.CurrentState = "MainMenu";
                await _context.SaveChangesAsync();
                return;
            }

            // Verify password
            var passwordValid = await _userManager.CheckPasswordAsync(student, password);
            if (!passwordValid)
            {
                await _whatsAppService.SendTextMessageAsync(phoneNumber,
                    "❌ Incorrect password. Please try again, or type *menu* to go back.");
                return; // Stay in LinkPassword state for retry
            }

            // Password verified — create the link
            var isFirst = !await _context.GuardianLinks.AnyAsync(gl => gl.PhoneNumber == phoneNumber);

            _context.GuardianLinks.Add(new GuardianLink
            {
                PhoneNumber = phoneNumber,
                StudentUserId = student.Id,
                IsPrimary = isFirst,
                LinkedAt = DateTime.UtcNow
            });

            session.CurrentState = "MainMenu";
            session.ContextData = null;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Guardian {Phone} linked to student {StudentId} ({Email})",
                phoneNumber, student.Id, student.Email);

            await _whatsAppService.SendTextMessageAsync(phoneNumber,
                $"✅ *Successfully linked!*\n\nYou are now connected to *{student.FullName ?? student.Email}*.\n\n" +
                "You can now view their performance and manage their subscription.");
            await SendMainMenuAsync(phoneNumber);
        }

        // ───────────────────────────── Student Selection ─────────────────────────────

        private async Task PromptStudentSelectionAsync(string phoneNumber, WhatsAppSession session, string nextAction)
        {
            var guardianLinks = await _context.GuardianLinks
                .Include(gl => gl.Student)
                    .ThenInclude(s => s.GradeLevel)
                .Where(gl => gl.PhoneNumber == phoneNumber)
                .ToListAsync();

            if (guardianLinks.Count == 0)
            {
                await _whatsAppService.SendTextMessageAsync(phoneNumber,
                    "You don't have any students linked to your account. Use 🔗 *Link Student* from the menu.");
                session.CurrentState = "MainMenu";
                await _context.SaveChangesAsync();
                await SendMainMenuAsync(phoneNumber);
            }
            else if (guardianLinks.Count == 1)
            {
                // Bypass selection — execute directly
                await ExecuteActionForStudentAsync(phoneNumber, session, nextAction, guardianLinks[0].StudentUserId);
            }
            else
            {
                session.CurrentState = "SelectStudent";
                session.ContextData = JsonSerializer.Serialize(new { NextAction = nextAction });
                await _context.SaveChangesAsync();

                var rows = guardianLinks.Take(10).Select(gl => new InteractiveListRow
                {
                    Id = $"student_{gl.StudentUserId}",
                    Title = TruncateTitle(gl.Student.FullName ?? gl.Student.Email ?? "Student"),
                    Description = gl.Student.GradeLevel?.Name ?? "Student"
                }).ToList();

                var sections = new List<InteractiveListSection>
                {
                    new InteractiveListSection
                    {
                        Title = "Your Students",
                        Rows = rows
                    }
                };

                await _whatsAppService.SendInteractiveListAsync(phoneNumber,
                    "Please select a student:", "Select Student", sections);
            }
        }

        private async Task HandleStudentSelectionAsync(string phoneNumber, WhatsAppSession session, string action)
        {
            if (action.StartsWith("student_"))
            {
                var studentId = action.Replace("student_", "");
                string nextAction = "MainMenu";
                if (!string.IsNullOrEmpty(session.ContextData))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(session.ContextData);
                        nextAction = doc.RootElement.GetProperty("NextAction").GetString() ?? "MainMenu";
                    }
                    catch { }
                }

                await ExecuteActionForStudentAsync(phoneNumber, session, nextAction, studentId);
            }
            else
            {
                await _whatsAppService.SendTextMessageAsync(phoneNumber, "Invalid selection. Going back to Main Menu.");
                session.CurrentState = "MainMenu";
                await _context.SaveChangesAsync();
                await SendMainMenuAsync(phoneNumber);
            }
        }

        private async Task ExecuteActionForStudentAsync(string phoneNumber, WhatsAppSession session, string nextAction, string studentId)
        {
            switch (nextAction)
            {
                case "view_performance":
                    await SendPerformanceDataAsync(phoneNumber, session, studentId);
                    break;

                case "pay_subscription":
                    await InitiatePaymentAsync(phoneNumber, session, studentId);
                    break;

                default:
                    session.CurrentState = "MainMenu";
                    await _context.SaveChangesAsync();
                    await SendMainMenuAsync(phoneNumber);
                    break;
            }
        }

        // ───────────────────────────── Performance Viewing ─────────────────────────────

        private async Task SendPerformanceDataAsync(string phoneNumber, WhatsAppSession session, string studentId)
        {
            var student = await _context.Users.FindAsync(studentId);
            if (student == null)
            {
                await _whatsAppService.SendTextMessageAsync(phoneNumber, "❌ Student not found.");
                session.CurrentState = "MainMenu";
                await _context.SaveChangesAsync();
                await SendMainMenuAsync(phoneNumber);
                return;
            }

            // Query identical to CurriculumController.GetPerformance()
            var rawSubjectGroups = await _context.QuizQuestionAttempts
                .Where(a => a.AppUserId == studentId)
                .Select(a => new
                {
                    SubjectId = a.QuizQuestion.Quiz.Unit != null
                        ? a.QuizQuestion.Quiz.Unit.Chapter.SubjectId
                        : (a.QuizQuestion.Quiz.SubjectId ?? 0),
                    IsCorrect = a.IsCorrect
                })
                .Where(x => x.SubjectId != 0)
                .GroupBy(x => x.SubjectId)
                .Select(g => new
                {
                    SubjectId = g.Key,
                    TotalAttempts = g.Count(),
                    Correct = g.Count(x => x.IsCorrect)
                })
                .ToListAsync();

            if (rawSubjectGroups.Count == 0)
            {
                await _whatsAppService.SendTextMessageAsync(phoneNumber,
                    $"📊 *Performance Report for {student.FullName ?? "Student"}*\n\n" +
                    "No quizzes have been completed yet. Encourage your child to start practising!");
                session.CurrentState = "MainMenu";
                await _context.SaveChangesAsync();
                await SendMainMenuAsync(phoneNumber);
                return;
            }

            var subjectIds = rawSubjectGroups.Select(g => g.SubjectId).ToList();
            var subjectNames = await _context.Subjects
                .Where(s => subjectIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.Name);

            var sb = new StringBuilder();
            sb.AppendLine($"📊 *Performance Report*");
            sb.AppendLine($"Student: *{student.FullName ?? "Student"}*");
            sb.AppendLine($"Grade: *{student.GradeLevel?.Name ?? "N/A"}*");
            sb.AppendLine();

            foreach (var group in rawSubjectGroups.OrderByDescending(g => g.TotalAttempts))
            {
                var name = subjectNames.TryGetValue(group.SubjectId, out var n) ? n : "Unknown";
                var percent = group.TotalAttempts > 0 ? (int)Math.Round((double)group.Correct / group.TotalAttempts * 100) : 0;
                var bar = GenerateProgressBar(percent);
                var emoji = percent >= 70 ? "🟢" : percent >= 40 ? "🟡" : "🔴";

                sb.AppendLine($"{emoji} *{name}*");
                sb.AppendLine($"   {bar} {percent}%");
                sb.AppendLine($"   {group.Correct}/{group.TotalAttempts} correct");
                sb.AppendLine();
            }

            // Calculate overall
            var totalCorrect = rawSubjectGroups.Sum(g => g.Correct);
            var totalAttempts = rawSubjectGroups.Sum(g => g.TotalAttempts);
            var overallPercent = totalAttempts > 0 ? (int)Math.Round((double)totalCorrect / totalAttempts * 100) : 0;

            sb.AppendLine($"━━━━━━━━━━━━━━━━━━");
            sb.AppendLine($"📈 *Overall: {overallPercent}%* ({totalCorrect}/{totalAttempts})");

            await _whatsAppService.SendTextMessageAsync(phoneNumber, sb.ToString());

            session.CurrentState = "MainMenu";
            await _context.SaveChangesAsync();
            await SendMainMenuAsync(phoneNumber);
        }

        private static string GenerateProgressBar(int percent)
        {
            var filled = (int)Math.Round(percent / 10.0);
            var empty = 10 - filled;
            return new string('▓', filled) + new string('░', empty);
        }

        // ───────────────────────────── Ecocash Payment ─────────────────────────────

        private async Task InitiatePaymentAsync(string phoneNumber, WhatsAppSession session, string studentId)
        {
            var student = await _context.Users
                .Include(u => u.GradeLevel)
                .FirstOrDefaultAsync(u => u.Id == studentId);

            if (student == null)
            {
                await _whatsAppService.SendTextMessageAsync(phoneNumber, "❌ Student not found.");
                session.CurrentState = "MainMenu";
                await _context.SaveChangesAsync();
                await SendMainMenuAsync(phoneNumber);
                return;
            }

            // Check if already subscribed
            if (student.IsSubscribed && student.SubscriptionEndDate > DateTime.UtcNow)
            {
                var endDate = student.SubscriptionEndDate?.ToString("dd MMM yyyy") ?? "N/A";
                await _whatsAppService.SendTextMessageAsync(phoneNumber,
                    $"✅ *{student.FullName}* already has an active subscription until *{endDate}*.\n\n" +
                    "No payment is needed right now.");
                session.CurrentState = "MainMenu";
                await _context.SaveChangesAsync();
                await SendMainMenuAsync(phoneNumber);
                return;
            }

            var tierPrice = student.GradeLevel?.TierPrice ?? 0;
            var durationDays = student.GradeLevel?.SubscriptionDurationDays > 0 ? student.GradeLevel.SubscriptionDurationDays : 90;

            if (tierPrice <= 0)
            {
                await _whatsAppService.SendTextMessageAsync(phoneNumber,
                    $"ℹ️ *{student.FullName}*'s grade level is free. No payment required!");
                session.CurrentState = "MainMenu";
                await _context.SaveChangesAsync();
                await SendMainMenuAsync(phoneNumber);
                return;
            }

            // Store payment context and ask for confirmation
            session.CurrentState = "PaySubscription";
            session.ContextData = JsonSerializer.Serialize(new
            {
                StudentId = studentId,
                StudentName = student.FullName ?? student.Email,
                Amount = tierPrice,
                DurationDays = durationDays,
                GradeName = student.GradeLevel?.Name ?? "Standard"
            });
            await _context.SaveChangesAsync();

            var buttons = new List<InteractiveButton>
            {
                new InteractiveButton { Id = "pay_confirm", Title = "✅ Pay Now" },
                new InteractiveButton { Id = "pay_cancel", Title = "❌ Cancel" }
            };

            await _whatsAppService.SendInteractiveButtonsAsync(phoneNumber,
                $"💰 *Subscription Payment*\n\n" +
                $"Student: *{student.FullName ?? "Student"}*\n" +
                $"Grade: *{student.GradeLevel?.Name ?? "Standard"}*\n" +
                $"Amount: *${tierPrice:0.00} USD*\n" +
                $"Duration: *{durationDays} Days*\n" +
                $"Method: *Ecocash*\n\n" +
                $"A USSD prompt will be sent to your phone. Please confirm?",
                buttons);
        }

        private async Task HandlePaymentConfirmationAsync(string phoneNumber, WhatsAppSession session, string action)
        {
            if (action == "pay_cancel")
            {
                await _whatsAppService.SendTextMessageAsync(phoneNumber, "Payment cancelled.");
                session.CurrentState = "MainMenu";
                session.ContextData = null;
                await _context.SaveChangesAsync();
                await SendMainMenuAsync(phoneNumber);
                return;
            }

            if (action != "pay_confirm")
            {
                await _whatsAppService.SendTextMessageAsync(phoneNumber,
                    "Please tap *Pay Now* to proceed or *Cancel* to go back.");
                return;
            }

            // Parse context
            if (string.IsNullOrEmpty(session.ContextData))
            {
                await _whatsAppService.SendTextMessageAsync(phoneNumber, "Something went wrong. Please try again from the menu.");
                session.CurrentState = "MainMenu";
                await _context.SaveChangesAsync();
                await SendMainMenuAsync(phoneNumber);
                return;
            }

            string studentId;
            decimal amount;
            int durationDays = 90;
            string studentName;
            try
            {
                using var doc = JsonDocument.Parse(session.ContextData);
                studentId = doc.RootElement.GetProperty("StudentId").GetString() ?? "";
                amount = doc.RootElement.GetProperty("Amount").GetDecimal();
                durationDays = doc.RootElement.TryGetProperty("DurationDays", out var dProp) ? dProp.GetInt32() : 90;
                studentName = doc.RootElement.GetProperty("StudentName").GetString() ?? "Student";
            }
            catch
            {
                await _whatsAppService.SendTextMessageAsync(phoneNumber, "Something went wrong. Please try again from the menu.");
                session.CurrentState = "MainMenu";
                await _context.SaveChangesAsync();
                await SendMainMenuAsync(phoneNumber);
                return;
            }

            var reference = $"EDCO-SUB-{studentId}-{DateTime.UtcNow.Ticks}";

            await _whatsAppService.SendTextMessageAsync(phoneNumber,
                "⏳ Processing your Ecocash payment...\nYou will receive a USSD prompt on your phone shortly. Please enter your PIN to confirm.");

            // Initiate Ecocash payment
            var result = await _ecocashService.InitiatePaymentAsync(
                phoneNumber,
                amount,
                reference,
                $"EdCo {durationDays}-Day Subscription for {studentName}");

            if (result.Success)
            {
                _logger.LogInformation("Ecocash payment initiated for student {StudentId}. TxId: {TxId}, Ref: {Ref}",
                    studentId, result.TransactionId, reference);

                await _whatsAppService.SendTextMessageAsync(phoneNumber,
                    "📱 *Ecocash payment initiated!*\n\n" +
                    "Please check your phone for the USSD prompt and enter your PIN to complete the payment.\n\n" +
                    $"Reference: `{reference}`\n" +
                    $"Amount: *${amount:0.00} USD*\n\n" +
                    "Your subscription will be activated automatically once payment is confirmed.");
            }
            else
            {
                _logger.LogWarning("Ecocash payment failed for student {StudentId}: {Error}", studentId, result.Error);

                await _whatsAppService.SendTextMessageAsync(phoneNumber,
                    "❌ *Payment failed*\n\n" +
                    $"Error: {result.Error}\n\n" +
                    "Please try again later or contact support. Type *menu* to go back.");
            }

            session.CurrentState = "MainMenu";
            session.ContextData = null;
            await _context.SaveChangesAsync();
        }

        // ───────────────────────────── Helpers ─────────────────────────────

        private static string TruncateTitle(string text)
        {
            // WhatsApp list row titles are limited to 24 characters
            return text.Length > 24 ? text.Substring(0, 24) : text;
        }
    }
}
