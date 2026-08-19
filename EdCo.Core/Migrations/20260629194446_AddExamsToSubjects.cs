using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EdCo.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddExamsToSubjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Quizzes_UnitId",
                table: "Quizzes");

            migrationBuilder.AlterColumn<int>(
                name: "UnitId",
                table: "Quizzes",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "DisplayPosition",
                table: "Quizzes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsExam",
                table: "Quizzes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SubjectId",
                table: "Quizzes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quizzes_SubjectId",
                table: "Quizzes",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Quizzes_UnitId",
                table: "Quizzes",
                column: "UnitId",
                unique: true,
                filter: "[UnitId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Quizzes_Subjects_SubjectId",
                table: "Quizzes",
                column: "SubjectId",
                principalTable: "Subjects",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Quizzes_Subjects_SubjectId",
                table: "Quizzes");

            migrationBuilder.DropIndex(
                name: "IX_Quizzes_SubjectId",
                table: "Quizzes");

            migrationBuilder.DropIndex(
                name: "IX_Quizzes_UnitId",
                table: "Quizzes");

            migrationBuilder.DropColumn(
                name: "DisplayPosition",
                table: "Quizzes");

            migrationBuilder.DropColumn(
                name: "IsExam",
                table: "Quizzes");

            migrationBuilder.DropColumn(
                name: "SubjectId",
                table: "Quizzes");

            migrationBuilder.AlterColumn<int>(
                name: "UnitId",
                table: "Quizzes",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quizzes_UnitId",
                table: "Quizzes",
                column: "UnitId",
                unique: true);
        }
    }
}
