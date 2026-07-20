using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BrokerApp.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSidebarVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VisibleSidebarItemsJson",
                table: "Users",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VisibleSidebarItemsJson",
                table: "Users");
        }
    }
}
