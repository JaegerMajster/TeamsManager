using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeamsManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceTeamIsVisibleWithVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsVisible",
                table: "Teams",
                newName: "Visibility");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Visibility",
                table: "Teams",
                newName: "IsVisible");
        }
    }
}
