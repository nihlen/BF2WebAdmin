using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BF2WebAdmin.Data.Migrations
{
    /// <inheritdoc />
    public partial class LayoutOwners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Layouts from before the builder had no map: they are Dalian's. One with the name of a Dalian layout keeps
            // its name with " (old)" (cut to fit 50 characters), so that names can be unique per map
            migrationBuilder.Sql("UPDATE MapMods SET Name = substr(Name, 1, 44) || ' (old)' WHERE Map IS NULL AND EXISTS (SELECT 1 FROM MapMods d WHERE d.Map = 'dalian_2_v_2' AND lower(d.Name) = lower(MapMods.Name))");
            migrationBuilder.Sql("UPDATE MapMods SET Map = 'dalian_2_v_2' WHERE Map IS NULL");

            migrationBuilder.AlterColumn<string>(
                name: "Map",
                table: "MapMods",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "MapMods",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MapMods_Map_Name",
                table: "MapMods",
                columns: new[] { "Map", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MapMods_Map_Name",
                table: "MapMods");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "MapMods");

            migrationBuilder.AlterColumn<string>(
                name: "Map",
                table: "MapMods",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");
        }
    }
}
