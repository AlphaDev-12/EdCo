using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EdCo.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexingAndRemainingSoftDeletes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "StudentProgresses",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "StudentProgresses",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "StudentProgresses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "StudentFlashcardProgresses",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "StudentFlashcardProgresses",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "StudentFlashcardProgresses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "QuizResults",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "QuizResults",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "QuizResults",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "QuizQuestionAttempts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "QuizQuestionAttempts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "QuizQuestionAttempts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "AiTutorSessions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "AiTutorSessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "AiTutorSessions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "AiTutorInteractions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "AiTutorInteractions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "AiTutorInteractions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_QuizResults_AttemptedAt",
                table: "QuizResults",
                column: "AttemptedAt");

            migrationBuilder.CreateIndex(
                name: "IX_QuizQuestionAttempts_AttemptedAt",
                table: "QuizQuestionAttempts",
                column: "AttemptedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_IsSubscribed",
                table: "AspNetUsers",
                column: "IsSubscribed");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QuizResults_AttemptedAt",
                table: "QuizResults");

            migrationBuilder.DropIndex(
                name: "IX_QuizQuestionAttempts_AttemptedAt",
                table: "QuizQuestionAttempts");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_IsSubscribed",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "StudentProgresses");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "StudentProgresses");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "StudentProgresses");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "StudentFlashcardProgresses");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "StudentFlashcardProgresses");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "StudentFlashcardProgresses");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "QuizResults");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "QuizResults");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "QuizResults");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "QuizQuestionAttempts");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "QuizQuestionAttempts");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "QuizQuestionAttempts");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "AiTutorSessions");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "AiTutorSessions");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "AiTutorSessions");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "AiTutorInteractions");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "AiTutorInteractions");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "AiTutorInteractions");
        }
    }
}
