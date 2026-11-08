using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BF2WebAdmin.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExportCursor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ChangeSeq",
                table: "Matches",
                type: "INTEGER",
                nullable: true);

            // Existing matches get export cursor numbers in start order
            migrationBuilder.Sql(
                @"UPDATE ""Matches"" SET ""ChangeSeq"" = (
                    SELECT COUNT(*) FROM ""Matches"" AS m2
                    WHERE m2.""MatchStart"" < ""Matches"".""MatchStart""
                       OR (m2.""MatchStart"" IS ""Matches"".""MatchStart"" AND m2.""Id"" <= ""Matches"".""Id"")
                       OR (m2.""MatchStart"" IS NULL AND ""Matches"".""MatchStart"" IS NOT NULL))");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_ChangeSeq",
                table: "Matches",
                column: "ChangeSeq",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Matches_ChangeSeq",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "ChangeSeq",
                table: "Matches");
        }
    }
}
