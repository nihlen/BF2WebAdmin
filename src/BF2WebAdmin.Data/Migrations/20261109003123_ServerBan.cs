using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BF2WebAdmin.Data.Migrations
{
    /// <inheritdoc />
    public partial class ServerBan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServerBan",
                columns: table => new
                {
                    ServerId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Identifier = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ByCdKey = table.Column<bool>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    BannedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    BannedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerBan", x => new { x.ServerId, x.Identifier });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServerBan");
        }
    }
}
