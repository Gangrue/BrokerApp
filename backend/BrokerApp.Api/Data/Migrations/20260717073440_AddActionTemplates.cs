using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BrokerApp.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddActionTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ActionTemplateItemId",
                table: "LoanActions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ActionTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    LoanType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActionTemplates_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ActionTemplateItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Section = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Priority = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DueOffsetDays = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionTemplateItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActionTemplateItems_ActionTemplates_ActionTemplateId",
                        column: x => x.ActionTemplateId,
                        principalTable: "ActionTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoanActions_ActionTemplateItemId",
                table: "LoanActions",
                column: "ActionTemplateItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ActionTemplateItems_ActionTemplateId",
                table: "ActionTemplateItems",
                column: "ActionTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ActionTemplateItems_OrganizationId_ActionTemplateId_SortOrder",
                table: "ActionTemplateItems",
                columns: new[] { "OrganizationId", "ActionTemplateId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ActionTemplates_OrganizationId_IsActive_LoanType_Stage",
                table: "ActionTemplates",
                columns: new[] { "OrganizationId", "IsActive", "LoanType", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_ActionTemplates_OrganizationId_Name",
                table: "ActionTemplates",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LoanActions_ActionTemplateItems_ActionTemplateItemId",
                table: "LoanActions",
                column: "ActionTemplateItemId",
                principalTable: "ActionTemplateItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LoanActions_ActionTemplateItems_ActionTemplateItemId",
                table: "LoanActions");

            migrationBuilder.DropTable(
                name: "ActionTemplateItems");

            migrationBuilder.DropTable(
                name: "ActionTemplates");

            migrationBuilder.DropIndex(
                name: "IX_LoanActions_ActionTemplateItemId",
                table: "LoanActions");

            migrationBuilder.DropColumn(
                name: "ActionTemplateItemId",
                table: "LoanActions");
        }
    }
}
