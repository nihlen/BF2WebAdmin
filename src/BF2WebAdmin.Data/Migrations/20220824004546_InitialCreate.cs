using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BF2WebAdmin.Data.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MapMods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EditedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapMods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Matches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ServerId = table.Column<string>(type: "TEXT", nullable: true),
                    ServerName = table.Column<string>(type: "TEXT", nullable: true),
                    Map = table.Column<string>(type: "TEXT", nullable: true),
                    Type = table.Column<string>(type: "TEXT", nullable: true),
                    TeamAHash = table.Column<string>(type: "TEXT", nullable: true),
                    TeamAName = table.Column<string>(type: "TEXT", nullable: true),
                    TeamAScore = table.Column<int>(type: "INTEGER", nullable: false),
                    TeamBHash = table.Column<string>(type: "TEXT", nullable: true),
                    TeamBName = table.Column<string>(type: "TEXT", nullable: true),
                    TeamBScore = table.Column<int>(type: "INTEGER", nullable: false),
                    MatchStart = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MatchEnd = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServerModules",
                columns: table => new
                {
                    ServerGroup = table.Column<string>(type: "TEXT", nullable: false),
                    Module = table.Column<string>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerModules", x => new { x.ServerGroup, x.Module });
                });

            migrationBuilder.CreateTable(
                name: "ServerPlayerAuths",
                columns: table => new
                {
                    ServerGroup = table.Column<string>(type: "TEXT", nullable: false),
                    PlayerHash = table.Column<string>(type: "TEXT", nullable: false),
                    AuthLevel = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerPlayerAuths", x => new { x.ServerGroup, x.PlayerHash });
                });

            migrationBuilder.CreateTable(
                name: "Servers",
                columns: table => new
                {
                    ServerId = table.Column<string>(type: "TEXT", nullable: false),
                    ServerGroup = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Servers", x => x.ServerId);
                });

            migrationBuilder.CreateTable(
                name: "MapModObjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TemplateName = table.Column<string>(type: "TEXT", nullable: true),
                    Position = table.Column<string>(type: "TEXT", nullable: true),
                    Rotation = table.Column<string>(type: "TEXT", nullable: true),
                    MapModId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapModObjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MapModObjects_MapMods_MapModId",
                        column: x => x.MapModId,
                        principalTable: "MapMods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MatchRounds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WinningTeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    PositionTrackerInterval = table.Column<double>(type: "REAL", nullable: false),
                    RoundStart = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RoundEnd = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MatchId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchRounds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchRounds_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MatchRoundPlayers",
                columns: table => new
                {
                    RoundId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlayerHash = table.Column<string>(type: "TEXT", nullable: false),
                    PlayerName = table.Column<string>(type: "TEXT", nullable: true),
                    TeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    SubVehicle = table.Column<string>(type: "TEXT", nullable: true),
                    SaidGo = table.Column<bool>(type: "INTEGER", nullable: false),
                    StartPosition = table.Column<string>(type: "TEXT", nullable: true),
                    DeathPosition = table.Column<string>(type: "TEXT", nullable: true),
                    DeathTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    KillerHash = table.Column<string>(type: "TEXT", nullable: true),
                    KillerWeapon = table.Column<string>(type: "TEXT", nullable: true),
                    KillerPosition = table.Column<string>(type: "TEXT", nullable: true),
                    MovementPathJson = table.Column<string>(type: "TEXT", nullable: true),
                    ProjectilePathsJson = table.Column<string>(type: "TEXT", nullable: true),
                    MatchId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchRoundPlayers", x => new { x.RoundId, x.PlayerHash });
                    table.ForeignKey(
                        name: "FK_MatchRoundPlayers_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchRoundPlayers_MatchRounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "MatchRounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MapModObjects_MapModId",
                table: "MapModObjects",
                column: "MapModId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchRoundPlayers_MatchId",
                table: "MatchRoundPlayers",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchRounds_MatchId",
                table: "MatchRounds",
                column: "MatchId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MapModObjects");

            migrationBuilder.DropTable(
                name: "MatchRoundPlayers");

            migrationBuilder.DropTable(
                name: "ServerModules");

            migrationBuilder.DropTable(
                name: "ServerPlayerAuths");

            migrationBuilder.DropTable(
                name: "Servers");

            migrationBuilder.DropTable(
                name: "MapMods");

            migrationBuilder.DropTable(
                name: "MatchRounds");

            migrationBuilder.DropTable(
                name: "Matches");
        }
    }
}
