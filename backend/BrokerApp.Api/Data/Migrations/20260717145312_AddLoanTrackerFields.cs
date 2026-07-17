using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BrokerApp.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanTrackerFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoBorrowerEmail",
                table: "Loans",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IcdSent",
                table: "Loans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IcdSigned",
                table: "Loans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LastContactDate",
                table: "Loans",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RealtorEmail",
                table: "Loans",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RealtorName",
                table: "Loans",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleContactEmail",
                table: "Loans",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleContactName",
                table: "Loans",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoBorrowerEmail",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "IcdSent",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "IcdSigned",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "LastContactDate",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "RealtorEmail",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "RealtorName",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "TitleContactEmail",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "TitleContactName",
                table: "Loans");
        }
    }
}
