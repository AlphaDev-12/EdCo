using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EdCo.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentToNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DocumentFileName",
                table: "NotesContents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentUrl",
                table: "NotesContents",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocumentFileName",
                table: "NotesContents");

            migrationBuilder.DropColumn(
                name: "DocumentUrl",
                table: "NotesContents");
        }
    }
}
