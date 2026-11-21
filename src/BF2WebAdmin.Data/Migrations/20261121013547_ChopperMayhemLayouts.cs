using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BF2WebAdmin.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChopperMayhemLayouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Chopper Mayhem's layouts (ChopperMayhemModule loads dalian_mayhem6 on Dalian Plant) are Dalian Plant's;
            // LayoutOwners gave every layout without a map dalian_2_v_2, and the builder finds layouts by the running map
            migrationBuilder.Sql("UPDATE MapMods SET Map = 'dalian_plant' WHERE Map = 'dalian_2_v_2' AND lower(Name) LIKE 'dalian_mayhem%'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE MapMods SET Map = 'dalian_2_v_2' WHERE Map = 'dalian_plant' AND lower(Name) LIKE 'dalian_mayhem%'");
        }
    }
}
