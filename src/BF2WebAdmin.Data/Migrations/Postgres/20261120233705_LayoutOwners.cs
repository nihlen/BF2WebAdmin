using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BF2WebAdmin.Data.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class LayoutOwners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Layouts from before the builder had no map: they are Dalian's. One with the name of a Dalian layout keeps
            // its name with " (old)" (cut to fit 50 characters), so that names can be unique per map
            migrationBuilder.Sql("UPDATE map_mod SET name = left(name, 44) || ' (old)' WHERE map IS NULL AND EXISTS (SELECT 1 FROM map_mod d WHERE d.map = 'dalian_2_v_2' AND lower(d.name) = lower(map_mod.name))");
            migrationBuilder.Sql("UPDATE map_mod SET map = 'dalian_2_v_2' WHERE map IS NULL");

            migrationBuilder.AlterColumn<string>(
                name: "map",
                table: "map_mod",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "owner_id",
                table: "map_mod",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_map_mod_map_name",
                table: "map_mod",
                columns: new[] { "map", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_map_mod_map_name",
                table: "map_mod");

            migrationBuilder.DropColumn(
                name: "owner_id",
                table: "map_mod");

            migrationBuilder.AlterColumn<string>(
                name: "map",
                table: "map_mod",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}
