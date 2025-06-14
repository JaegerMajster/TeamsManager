using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeamsManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemDefaultFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSystemDefault",
                table: "OrganizationalUnits",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSystemDefault",
                table: "Departments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationalUnits_IsSystemDefault",
                table: "OrganizationalUnits",
                column: "IsSystemDefault");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_IsSystemDefault",
                table: "Departments",
                column: "IsSystemDefault");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrganizationalUnits_IsSystemDefault",
                table: "OrganizationalUnits");

            migrationBuilder.DropIndex(
                name: "IX_Departments_IsSystemDefault",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "IsSystemDefault",
                table: "OrganizationalUnits");

            migrationBuilder.DropColumn(
                name: "IsSystemDefault",
                table: "Departments");
        }
    }
}
