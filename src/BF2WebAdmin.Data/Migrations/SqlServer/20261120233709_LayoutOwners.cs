using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BF2WebAdmin.Data.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class LayoutOwners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Layouts from before the builder had no map: they are Dalian's. One with the name of a Dalian layout keeps
            // its name with " (old)" (cut to fit 50 characters), so that names can be unique per map
            migrationBuilder.Sql("UPDATE m SET Name = LEFT(m.Name, 44) + N' (old)' FROM dbo.MapMod m WHERE m.Map IS NULL AND EXISTS (SELECT 1 FROM dbo.MapMod d WHERE d.Map = N'dalian_2_v_2' AND LOWER(d.Name) = LOWER(m.Name))");
            migrationBuilder.Sql("UPDATE dbo.MapMod SET Map = N'dalian_2_v_2' WHERE Map IS NULL");

            migrationBuilder.AlterColumn<string>(
                name: "Map",
                schema: "dbo",
                table: "MapMod",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                schema: "dbo",
                table: "MapMod",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MapMod_Map_Name",
                schema: "dbo",
                table: "MapMod",
                columns: new[] { "Map", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MapMod_Map_Name",
                schema: "dbo",
                table: "MapMod");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                schema: "dbo",
                table: "MapMod");

            migrationBuilder.AlterColumn<string>(
                name: "Map",
                schema: "dbo",
                table: "MapMod",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);
        }
    }
}
