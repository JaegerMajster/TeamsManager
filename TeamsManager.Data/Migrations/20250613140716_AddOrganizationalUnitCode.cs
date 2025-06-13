using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeamsManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationalUnitCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "OrganizationalUnits",
                type: "TEXT",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Code",
                table: "OrganizationalUnits");
        }
    }
}
