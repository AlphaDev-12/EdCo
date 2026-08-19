using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EdCo.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddQuantitativeAiTutor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SubjectType",
                table: "Subjects",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AiTutorSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    SubjectId = table.Column<int>(type: "int", nullable: false),
                    Topic = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastInteractionAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiTutorSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiTutorSessions_AspNetUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AiTutorSessions_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AiTutorInteractions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserMessage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MathExpressionLatex = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UploadedImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiResponse = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequiresGraphRender = table.Column<bool>(type: "bit", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiTutorInteractions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiTutorInteractions_AiTutorSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AiTutorSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiTutorInteractions_SessionId",
                table: "AiTutorInteractions",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AiTutorSessions_AppUserId",
                table: "AiTutorSessions",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AiTutorSessions_SubjectId",
                table: "AiTutorSessions",
                column: "SubjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiTutorInteractions");

            migrationBuilder.DropTable(
                name: "AiTutorSessions");

            migrationBuilder.DropColumn(
                name: "SubjectType",
                table: "Subjects");
        }
    }
}
