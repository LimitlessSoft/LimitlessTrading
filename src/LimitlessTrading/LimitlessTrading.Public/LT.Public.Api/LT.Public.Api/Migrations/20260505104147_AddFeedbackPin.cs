using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LT.Public.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedbackPin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPinned",
                table: "Feedback",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPinned",
                table: "Feedback");
        }
    }
}
