using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeamsManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationalUnit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrganizationalUnitId",
                table: "Departments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrganizationalUnits",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ParentUnitId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationalUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationalUnits_OrganizationalUnits_ParentUnitId",
                        column: x => x.ParentUnitId,
                        principalTable: "OrganizationalUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Departments_OrganizationalUnitId",
                table: "Departments",
                column: "OrganizationalUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationalUnits_CreatedBy",
                table: "OrganizationalUnits",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationalUnits_CreatedDate",
                table: "OrganizationalUnits",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationalUnits_IsActive",
                table: "OrganizationalUnits",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationalUnits_Name",
                table: "OrganizationalUnits",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationalUnits_ParentUnitId",
                table: "OrganizationalUnits",
                column: "ParentUnitId");

            migrationBuilder.AddForeignKey(
                name: "FK_Departments_OrganizationalUnits_OrganizationalUnitId",
                table: "Departments",
                column: "OrganizationalUnitId",
                principalTable: "OrganizationalUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Departments_OrganizationalUnits_OrganizationalUnitId",
                table: "Departments");

            migrationBuilder.DropTable(
                name: "OrganizationalUnits");

            migrationBuilder.DropIndex(
                name: "IX_Departments_OrganizationalUnitId",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "OrganizationalUnitId",
                table: "Departments");
        }
    }
}
