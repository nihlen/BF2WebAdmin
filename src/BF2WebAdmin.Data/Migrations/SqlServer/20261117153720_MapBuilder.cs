using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BF2WebAdmin.Data.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class MapBuilder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Kind",
                schema: "dbo",
                table: "MapModObject",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Team",
                schema: "dbo",
                table: "MapModObject",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Map",
                schema: "dbo",
                table: "MapMod",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "dbo",
                table: "MapModObject");

            migrationBuilder.DropColumn(
                name: "Team",
                schema: "dbo",
                table: "MapModObject");

            migrationBuilder.DropColumn(
                name: "Map",
                schema: "dbo",
                table: "MapMod");
        }
    }
}
