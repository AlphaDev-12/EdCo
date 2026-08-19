using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using EdCo.Core.Entities;

namespace EdCo.Core.Data
{
    public class EdCoDbContext : IdentityDbContext<AppUser>
    {
        public EdCoDbContext(DbContextOptions<EdCoDbContext> options)
            : base(options)
        {
        }

        public DbSet<GradeLevel> GradeLevels { get; set; } = null!;
        public DbSet<Subject> Subjects { get; set; } = null!;
        public DbSet<Chapter> Chapters { get; set; } = null!;
        public DbSet<Unit> Units { get; set; } = null!;
        public DbSet<VideoAsset> VideoAssets { get; set; } = null!;
        public DbSet<NotesContent> NotesContents { get; set; } = null!;
        public DbSet<Quiz> Quizzes { get; set; } = null!;
        public DbSet<QuizQuestion> QuizQuestions { get; set; } = null!;
        public DbSet<StudentProgress> StudentProgresses { get; set; } = null!;
        public DbSet<QuizResult> QuizResults { get; set; } = null!;
        public DbSet<AiInteractionLog> AiInteractionLogs { get; set; } = null!;
        public DbSet<Flashcard> Flashcards { get; set; } = null!;
        public DbSet<StudentFlashcardProgress> StudentFlashcardProgresses { get; set; } = null!;
        public DbSet<QuizQuestionAttempt> QuizQuestionAttempts { get; set; } = null!;
        public DbSet<AiTutorSession> AiTutorSessions { get; set; } = null!;
        public DbSet<AiTutorInteraction> AiTutorInteractions { get; set; } = null!;
        public DbSet<AuditLog> AuditLogs { get; set; } = null!;
        public DbSet<StudentActivityLog> StudentActivityLogs { get; set; } = null!;
        public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
        public DbSet<GuardianLink> GuardianLinks { get; set; } = null!;
        public DbSet<WhatsAppSession> WhatsAppSessions { get; set; } = null!;
        public DbSet<AppErrorLog> ErrorLogs { get; set; } = null!;
        public DbSet<AiApiKey> AiApiKeys { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure RefreshToken entity indexes
            builder.Entity<RefreshToken>().HasIndex(rt => rt.TokenHash).IsUnique();
            builder.Entity<RefreshToken>().HasIndex(rt => rt.UserId);

            // Configure AppErrorLog indexes for fast dashboard queries
            builder.Entity<AppErrorLog>().HasIndex(el => el.CreatedAt);
            builder.Entity<AppErrorLog>().HasIndex(el => el.Source);
            builder.Entity<AppErrorLog>().HasIndex(el => el.LogLevel);
            builder.Entity<AppErrorLog>().HasIndex(el => el.IsResolved);

            // Configure AiApiKey indexes
            builder.Entity<AiApiKey>().HasIndex(k => k.Provider);
            builder.Entity<AiApiKey>().HasIndex(k => k.IsActive);

            // Global Query Filters for Soft Delete
            builder.Entity<GradeLevel>().HasQueryFilter(e => !e.IsDeleted);
            builder.Entity<Subject>().HasQueryFilter(e => !e.IsDeleted);
            builder.Entity<Chapter>().HasQueryFilter(e => !e.IsDeleted);
            builder.Entity<Unit>().HasQueryFilter(e => !e.IsDeleted);
            builder.Entity<VideoAsset>().HasQueryFilter(e => !e.IsDeleted);
            builder.Entity<NotesContent>().HasQueryFilter(e => !e.IsDeleted);
            builder.Entity<Quiz>().HasQueryFilter(e => !e.IsDeleted);
            builder.Entity<QuizQuestion>().HasQueryFilter(e => !e.IsDeleted);
            builder.Entity<Flashcard>().HasQueryFilter(e => !e.IsDeleted);
            builder.Entity<QuizResult>().HasQueryFilter(e => !e.IsDeleted);
            builder.Entity<StudentProgress>().HasQueryFilter(e => !e.IsDeleted);
            builder.Entity<StudentFlashcardProgress>().HasQueryFilter(e => !e.IsDeleted);
            builder.Entity<QuizQuestionAttempt>().HasQueryFilter(e => !e.IsDeleted);
            builder.Entity<AiTutorSession>().HasQueryFilter(e => !e.IsDeleted);
            builder.Entity<AiTutorInteraction>().HasQueryFilter(e => !e.IsDeleted);
            builder.Entity<GuardianLink>().HasQueryFilter(e => !e.IsDeleted);
            builder.Entity<AiApiKey>().HasQueryFilter(e => !e.IsDeleted);

            // Database Indices for High-Frequency Query Columns (UnitId, SubjectId, AttemptedAt, IsSubscribed)
            builder.Entity<StudentProgress>().HasIndex(sp => sp.UnitId);
            builder.Entity<Quiz>().HasIndex(q => q.UnitId);
            builder.Entity<Flashcard>().HasIndex(f => f.UnitId);
            builder.Entity<VideoAsset>().HasIndex(v => v.UnitId);
            builder.Entity<NotesContent>().HasIndex(n => n.UnitId);

            builder.Entity<Chapter>().HasIndex(c => c.SubjectId);
            builder.Entity<Quiz>().HasIndex(q => q.SubjectId);
            builder.Entity<AiTutorSession>().HasIndex(ts => ts.SubjectId);

            builder.Entity<QuizResult>().HasIndex(qr => qr.AttemptedAt);
            builder.Entity<QuizQuestionAttempt>().HasIndex(qqa => qqa.AttemptedAt);

            builder.Entity<AppUser>().HasIndex(u => u.IsSubscribed);

            builder.Entity<GuardianLink>().HasIndex(gl => gl.PhoneNumber);
            builder.Entity<WhatsAppSession>().HasIndex(ws => ws.PhoneNumber).IsUnique();

            // Configure AppUser -> GradeLevel
            builder.Entity<AppUser>()
                .HasOne(u => u.GradeLevel)
                .WithMany()
                .HasForeignKey(u => u.GradeLevelId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure GradeLevel -> Subject
            builder.Entity<Subject>()
                .HasOne(s => s.GradeLevel)
                .WithMany(g => g.Subjects)
                .HasForeignKey(s => s.GradeLevelId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Subject -> Chapter
            builder.Entity<Chapter>()
                .HasOne(c => c.Subject)
                .WithMany(s => s.Chapters)
                .HasForeignKey(c => c.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Chapter -> Unit
            builder.Entity<Unit>()
                .HasOne(u => u.Chapter)
                .WithMany(c => c.Units)
                .HasForeignKey(u => u.ChapterId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Unit -> VideoAsset (One-to-One)
            builder.Entity<VideoAsset>()
                .HasOne(v => v.Unit)
                .WithOne(u => u.Video)
                .HasForeignKey<VideoAsset>(v => v.UnitId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Unit -> NotesContent (One-to-One)
            builder.Entity<NotesContent>()
                .HasOne(n => n.Unit)
                .WithOne(u => u.Notes)
                .HasForeignKey<NotesContent>(n => n.UnitId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Unit -> Quiz (One-to-One)
            builder.Entity<Quiz>()
                .HasOne(q => q.Unit)
                .WithOne(u => u.Quiz)
                .HasForeignKey<Quiz>(q => q.UnitId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Quiz -> QuizQuestion
            builder.Entity<QuizQuestion>()
                .HasOne(qq => qq.Quiz)
                .WithMany(q => q.Questions)
                .HasForeignKey(qq => qq.QuizId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure StudentProgress
            builder.Entity<StudentProgress>()
                .HasOne(sp => sp.AppUser)
                .WithMany()
                .HasForeignKey(sp => sp.AppUserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<StudentProgress>()
                .HasOne(sp => sp.Unit)
                .WithMany()
                .HasForeignKey(sp => sp.UnitId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure QuizResult
            builder.Entity<QuizResult>()
                .HasOne(qr => qr.AppUser)
                .WithMany()
                .HasForeignKey(qr => qr.AppUserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<QuizResult>()
                .HasOne(qr => qr.Quiz)
                .WithMany()
                .HasForeignKey(qr => qr.QuizId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Flashcard
            builder.Entity<Flashcard>()
                .HasOne(f => f.Unit)
                .WithMany()
                .HasForeignKey(f => f.UnitId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure StudentFlashcardProgress
            builder.Entity<StudentFlashcardProgress>()
                .HasOne(sfp => sfp.AppUser)
                .WithMany()
                .HasForeignKey(sfp => sfp.AppUserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<StudentFlashcardProgress>()
                .HasOne(sfp => sfp.Flashcard)
                .WithMany()
                .HasForeignKey(sfp => sfp.FlashcardId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure QuizQuestionAttempt
            builder.Entity<QuizQuestionAttempt>()
                .HasOne(qqa => qqa.AppUser)
                .WithMany()
                .HasForeignKey(qqa => qqa.AppUserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<QuizQuestionAttempt>()
                .HasOne(qqa => qqa.QuizQuestion)
                .WithMany()
                .HasForeignKey(qqa => qqa.QuizQuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure AiTutorSession
            builder.Entity<AiTutorSession>()
                .HasOne(ts => ts.AppUser)
                .WithMany()
                .HasForeignKey(ts => ts.AppUserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<AiTutorSession>()
                .HasOne(ts => ts.Subject)
                .WithMany()
                .HasForeignKey(ts => ts.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure AiTutorInteraction
            // Configure GuardianLink -> AppUser (Student)
            builder.Entity<GuardianLink>()
                .HasOne(gl => gl.Student)
                .WithMany()
                .HasForeignKey(gl => gl.StudentUserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<AiTutorInteraction>()
                .HasOne(ti => ti.Session)
                .WithMany(ts => ts.Interactions)
                .HasForeignKey(ti => ti.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        }

        public override int SaveChanges()
        {
            ApplySoftDelete();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ApplySoftDelete();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void ApplySoftDelete()
        {
            foreach (var entry in ChangeTracker.Entries<ISoftDelete>())
            {
                if (entry.State == EntityState.Deleted)
                {
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.DeletedAt = System.DateTime.UtcNow;
                }
            }
        }
    }
}
