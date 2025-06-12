using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeamsManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamDepartmentRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DepartmentId",
                table: "Teams",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teams_DepartmentId",
                table: "Teams",
                column: "DepartmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Teams_Departments_DepartmentId",
                table: "Teams",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Teams_Departments_DepartmentId",
                table: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_Teams_DepartmentId",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "Teams");
        }
    }
}
