using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BF2WebAdmin.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdminChannel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdminKey",
                table: "Servers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTestServer",
                table: "Servers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdminKey",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "IsTestServer",
                table: "Servers");
        }
    }
}
