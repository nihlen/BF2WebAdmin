using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BF2WebAdmin.Data.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class ServerBan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "server_ban",
                columns: table => new
                {
                    server_id = table.Column<string>(type: "text", nullable: false),
                    identifier = table.Column<string>(type: "text", nullable: false),
                    by_cd_key = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "text", nullable: true),
                    banned_by = table.Column<string>(type: "text", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    banned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_server_ban", x => new { x.server_id, x.identifier });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "server_ban");
        }
    }
}
