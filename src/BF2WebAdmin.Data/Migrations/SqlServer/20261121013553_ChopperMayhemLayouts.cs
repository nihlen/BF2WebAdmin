using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BF2WebAdmin.Data.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class ChopperMayhemLayouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Chopper Mayhem's layouts (ChopperMayhemModule loads dalian_mayhem6 on Dalian Plant) are Dalian Plant's;
            // LayoutOwners gave every layout without a map dalian_2_v_2, and the builder finds layouts by the running map
            migrationBuilder.Sql("UPDATE dbo.MapMod SET Map = N'dalian_plant' WHERE Map = N'dalian_2_v_2' AND LOWER(Name) LIKE N'dalian_mayhem%'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE dbo.MapMod SET Map = N'dalian_2_v_2' WHERE Map = N'dalian_plant' AND LOWER(Name) LIKE N'dalian_mayhem%'");
        }
    }
}
