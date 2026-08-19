using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EdCo.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddModelUsedAndCostToAiLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Cost",
                table: "AiInteractionLogs",
                type: "decimal(18,8)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ModelUsed",
                table: "AiInteractionLogs",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Cost",
                table: "AiInteractionLogs");

            migrationBuilder.DropColumn(
                name: "ModelUsed",
                table: "AiInteractionLogs");
        }
    }
}
