using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BF2WebAdmin.Data.Migrations
{
    /// <inheritdoc />
    public partial class JoinHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JoinHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Guid = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    IpAddress = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    ServerIpAddress = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    JoinDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsLikelySpoof = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JoinHistory", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JoinHistory");
        }
    }
}
