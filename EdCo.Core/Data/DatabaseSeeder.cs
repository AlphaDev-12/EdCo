using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EdCo.Core.Entities;

namespace EdCo.Core.Data
{
    public static class DatabaseSeeder
    {
        public static async Task SeedAsync(EdCoDbContext context)
        {
            // Seed Grade Levels if empty
            if (!await context.GradeLevels.AnyAsync())
            {
                var grade10 = new GradeLevel { Name = "Grade 10 - Secondary", TierPrice = 19.99m, SubscriptionDurationDays = 90, IsActive = true };
                var grade11 = new GradeLevel { Name = "Grade 11 - Secondary", TierPrice = 24.99m, SubscriptionDurationDays = 90, IsActive = true };
                var grade12 = new GradeLevel { Name = "Grade 12 - Senior Secondary", TierPrice = 29.99m, SubscriptionDurationDays = 90, IsActive = true };

                context.GradeLevels.AddRange(grade10, grade11, grade12);
                await context.SaveChangesAsync();

                // Seed Subjects for Grade 10
                var math = new Subject { Name = "Mathematics & Algebra", SubjectType = SubjectType.Quantitative, GradeLevelId = grade10.Id };
                var physics = new Subject { Name = "Physics Fundamentals", SubjectType = SubjectType.Quantitative, GradeLevelId = grade10.Id };
                var english = new Subject { Name = "English Literature", SubjectType = SubjectType.Humanities, GradeLevelId = grade10.Id };

                context.Subjects.AddRange(math, physics, english);
                await context.SaveChangesAsync();

                // Seed Chapter & Unit for Mathematics
                var chapter1 = new Chapter { Title = "Algebraic Expressions & Functions", OrderIndex = 1, SubjectId = math.Id };
                context.Chapters.Add(chapter1);
                await context.SaveChangesAsync();

                var unit1 = new Unit { Title = "Quadratic Equations", OrderIndex = 1, ChapterId = chapter1.Id };
                context.Units.Add(unit1);
                await context.SaveChangesAsync();

                // Seed Sample Quiz
                var quiz = new Quiz
                {
                    Title = "Quadratic Equations Assessment",
                    UnitId = unit1.Id,
                    SubjectId = math.Id,
                    IsExam = false,
                    DisplayPosition = 1
                };
                context.Quizzes.Add(quiz);
                await context.SaveChangesAsync();

                // Seed Sample Questions
                var q1 = new QuizQuestion
                {
                    QuizId = quiz.Id,
                    QuestionText = "Solve for x: x^2 - 5x + 6 = 0",
                    OptionA = "x = 2, x = 3",
                    OptionB = "x = 1, x = 6",
                    OptionC = "x = -2, x = -3",
                    OptionD = "x = 0, x = 5",
                    CorrectOption = "A",
                    Points = 5,
                    QuestionType = QuestionType.MultipleChoice
                };

                var q2 = new QuizQuestion
                {
                    QuizId = quiz.Id,
                    QuestionText = "Derive the discriminant of 2x^2 + 4x + 2 = 0 and explain the nature of its roots.",
                    CorrectAnswer = "b^2 - 4ac = 16 - 16 = 0. Exactly one real double root.",
                    RubricJson = "{\"criteria\":\"Discriminant calculation accuracy\", \"maxPoints\":10}",
                    Points = 10,
                    QuestionType = QuestionType.ShortAnswer
                };

                context.QuizQuestions.AddRange(q1, q2);
                await context.SaveChangesAsync();
            }
        }
    }
}
