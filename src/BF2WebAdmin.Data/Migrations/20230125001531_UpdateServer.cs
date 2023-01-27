using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BF2WebAdmin.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiscordAdminChannel",
                table: "Servers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscordBotToken",
                table: "Servers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscordMatchResultChannel",
                table: "Servers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscordNotificationChannel",
                table: "Servers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GamePort",
                table: "Servers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "IpAddress",
                table: "Servers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QueryPort",
                table: "Servers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RconPassword",
                table: "Servers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RconPort",
                table: "Servers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscordAdminChannel",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "DiscordBotToken",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "DiscordMatchResultChannel",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "DiscordNotificationChannel",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "GamePort",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "IpAddress",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "QueryPort",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "RconPassword",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "RconPort",
                table: "Servers");
        }
    }
}
