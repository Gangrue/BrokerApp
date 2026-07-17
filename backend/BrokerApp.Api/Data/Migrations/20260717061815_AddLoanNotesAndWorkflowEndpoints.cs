using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BrokerApp.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanNotesAndWorkflowEndpoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LoanNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoanActionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoanNotes_LoanActions_LoanActionId",
                        column: x => x.LoanActionId,
                        principalTable: "LoanActions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoanNotes_Loans_LoanId",
                        column: x => x.LoanId,
                        principalTable: "Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoanNotes_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoanNotes_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoanNotes_CreatedByUserId",
                table: "LoanNotes",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LoanNotes_LoanActionId",
                table: "LoanNotes",
                column: "LoanActionId");

            migrationBuilder.CreateIndex(
                name: "IX_LoanNotes_LoanId",
                table: "LoanNotes",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_LoanNotes_OrganizationId_LoanId_CreatedAtUtc",
                table: "LoanNotes",
                columns: new[] { "OrganizationId", "LoanId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoanNotes");
        }
    }
}
