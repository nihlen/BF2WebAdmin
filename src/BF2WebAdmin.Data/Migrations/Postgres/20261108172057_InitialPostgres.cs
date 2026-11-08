using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BF2WebAdmin.Data.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class InitialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "match_change_seq");

            migrationBuilder.CreateTable(
                name: "map_mod",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    edited_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_map_mod", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "match",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    server_id = table.Column<string>(type: "text", nullable: true),
                    server_name = table.Column<string>(type: "text", nullable: true),
                    map = table.Column<string>(type: "text", nullable: true),
                    type = table.Column<string>(type: "text", nullable: true),
                    team_a_hash = table.Column<string>(type: "text", nullable: true),
                    team_a_name = table.Column<string>(type: "text", nullable: true),
                    team_a_score = table.Column<int>(type: "integer", nullable: false),
                    team_b_hash = table.Column<string>(type: "text", nullable: true),
                    team_b_name = table.Column<string>(type: "text", nullable: true),
                    team_b_score = table.Column<int>(type: "integer", nullable: false),
                    match_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    match_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    change_seq = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_match", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "player_join",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    guid = table.Column<string>(type: "text", nullable: false),
                    ip_address = table.Column<string>(type: "text", nullable: false),
                    server_ip_address = table.Column<string>(type: "text", nullable: false),
                    join_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_join", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "server",
                columns: table => new
                {
                    server_id = table.Column<string>(type: "text", nullable: false),
                    server_group = table.Column<string>(type: "text", nullable: true),
                    ip_address = table.Column<string>(type: "text", nullable: true),
                    game_port = table.Column<int>(type: "integer", nullable: false),
                    query_port = table.Column<int>(type: "integer", nullable: false),
                    rcon_port = table.Column<int>(type: "integer", nullable: false),
                    rcon_password = table.Column<string>(type: "text", nullable: true),
                    discord_bot_token = table.Column<string>(type: "text", nullable: true),
                    discord_admin_channel = table.Column<string>(type: "text", nullable: true),
                    discord_notification_channel = table.Column<string>(type: "text", nullable: true),
                    discord_match_result_channel = table.Column<string>(type: "text", nullable: true),
                    admin_key = table.Column<string>(type: "text", nullable: true),
                    is_test_server = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_server", x => x.server_id);
                });

            migrationBuilder.CreateTable(
                name: "server_module",
                columns: table => new
                {
                    server_group = table.Column<string>(type: "text", nullable: false),
                    module = table.Column<string>(type: "text", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_server_module", x => new { x.server_group, x.module });
                });

            migrationBuilder.CreateTable(
                name: "server_player_auth",
                columns: table => new
                {
                    server_group = table.Column<string>(type: "text", nullable: false),
                    player_hash = table.Column<string>(type: "text", nullable: false),
                    auth_level = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_server_player_auth", x => new { x.server_group, x.player_hash });
                });

            migrationBuilder.CreateTable(
                name: "map_mod_object",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_name = table.Column<string>(type: "text", nullable: true),
                    position = table.Column<string>(type: "text", nullable: true),
                    rotation = table.Column<string>(type: "text", nullable: true),
                    map_mod_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_map_mod_object", x => x.id);
                    table.ForeignKey(
                        name: "fk_map_mod_object_map_mod_map_mod_id",
                        column: x => x.map_mod_id,
                        principalTable: "map_mod",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "match_round",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    winning_team_id = table.Column<int>(type: "integer", nullable: false),
                    position_tracker_interval = table.Column<decimal>(type: "numeric(10,5)", nullable: false),
                    round_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    round_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    match_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_match_round", x => x.id);
                    table.ForeignKey(
                        name: "fk_match_round_match_match_id",
                        column: x => x.match_id,
                        principalTable: "match",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "match_round_player",
                columns: table => new
                {
                    round_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_hash = table.Column<string>(type: "text", nullable: false),
                    player_name = table.Column<string>(type: "text", nullable: true),
                    team_id = table.Column<int>(type: "integer", nullable: false),
                    sub_vehicle = table.Column<string>(type: "text", nullable: true),
                    said_go = table.Column<bool>(type: "boolean", nullable: false),
                    start_position = table.Column<string>(type: "text", nullable: true),
                    death_position = table.Column<string>(type: "text", nullable: true),
                    death_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    killer_hash = table.Column<string>(type: "text", nullable: true),
                    killer_weapon = table.Column<string>(type: "text", nullable: true),
                    killer_position = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_match_round_player", x => new { x.round_id, x.player_hash });
                    table.ForeignKey(
                        name: "fk_match_round_player_match_round_round_id",
                        column: x => x.round_id,
                        principalTable: "match_round",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "match_round_player_path",
                columns: table => new
                {
                    round_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_hash = table.Column<string>(type: "text", nullable: false),
                    movement_path = table.Column<byte[]>(type: "bytea", nullable: true),
                    projectile_paths = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_match_round_player_path", x => new { x.round_id, x.player_hash });
                    table.ForeignKey(
                        name: "fk_match_round_player_path_match_round_player_round_id_player_",
                        columns: x => new { x.round_id, x.player_hash },
                        principalTable: "match_round_player",
                        principalColumns: new[] { "round_id", "player_hash" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_map_mod_object_map_mod_id",
                table: "map_mod_object",
                column: "map_mod_id");

            migrationBuilder.CreateIndex(
                name: "ix_match_change_seq",
                table: "match",
                column: "change_seq",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_match_match_start",
                table: "match",
                column: "match_start",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_match_round_match_id_round_start",
                table: "match_round",
                columns: new[] { "match_id", "round_start" });

            migrationBuilder.CreateIndex(
                name: "ix_match_round_player_player_hash",
                table: "match_round_player",
                column: "player_hash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "map_mod_object");

            migrationBuilder.DropTable(
                name: "match_round_player_path");

            migrationBuilder.DropTable(
                name: "player_join");

            migrationBuilder.DropTable(
                name: "server");

            migrationBuilder.DropTable(
                name: "server_module");

            migrationBuilder.DropTable(
                name: "server_player_auth");

            migrationBuilder.DropTable(
                name: "map_mod");

            migrationBuilder.DropTable(
                name: "match_round_player");

            migrationBuilder.DropTable(
                name: "match_round");

            migrationBuilder.DropTable(
                name: "match");

            migrationBuilder.DropSequence(
                name: "match_change_seq");
        }
    }
}
