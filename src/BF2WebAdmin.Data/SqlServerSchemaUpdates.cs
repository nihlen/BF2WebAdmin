using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BF2WebAdmin.Data;

/// <summary>
/// Schema changes WebAdmin applies to an existing SQL Server database at startup, each one idempotent. Schema.sql has
/// the same statements for a new database. (SQLite gets its changes from the EF migrations.)
/// </summary>
public static class SqlServerSchemaUpdates
{
    /// <summary>The export cursor on dbo.Match (see <see cref="Entities.Match.ChangeSeq"/>), in batches as in Schema.sql.</summary>
    public static readonly string[] ExportCursor =
    {
        @"IF COL_LENGTH(N'[dbo].[Match]', N'ChangeSeq') IS NULL
	ALTER TABLE [dbo].[Match] ADD [ChangeSeq] [bigint] NULL;",

        @"IF NOT EXISTS (SELECT * FROM sys.sequences WHERE object_id = OBJECT_ID(N'[dbo].[MatchChangeSeq]'))
	CREATE SEQUENCE [dbo].[MatchChangeSeq] AS [bigint] START WITH 1 INCREMENT BY 1;",

        // Existing matches get numbers in start order, after any number already given; the sequence then continues above all of them
        @"IF EXISTS (SELECT * FROM [dbo].[Match] WHERE [ChangeSeq] IS NULL)
BEGIN
	DECLARE @Base bigint = (SELECT ISNULL(MAX([ChangeSeq]), 0) FROM [dbo].[Match]);
	WITH Numbered AS (SELECT [ChangeSeq], ROW_NUMBER() OVER (ORDER BY [MatchStart], [Id]) AS [RowNumber] FROM [dbo].[Match] WHERE [ChangeSeq] IS NULL)
	UPDATE Numbered SET [ChangeSeq] = @Base + [RowNumber];
	DECLARE @Next bigint = (SELECT MAX([ChangeSeq]) + 1 FROM [dbo].[Match]);
	DECLARE @Current bigint = (SELECT CAST([current_value] AS bigint) FROM sys.sequences WHERE object_id = OBJECT_ID(N'[dbo].[MatchChangeSeq]'));
	IF @Current >= @Next SET @Next = @Current + 1;
	DECLARE @Restart nvarchar(200) = N'ALTER SEQUENCE [dbo].[MatchChangeSeq] RESTART WITH ' + CAST(@Next AS nvarchar(20));
	EXEC (@Restart);
END",

        @"IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Match]') AND name = N'IX_Match_ChangeSeq')
	CREATE UNIQUE NONCLUSTERED INDEX [IX_Match_ChangeSeq] ON [dbo].[Match] ([ChangeSeq]) WHERE [ChangeSeq] IS NOT NULL;",
    };

    public static async Task ApplyAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        foreach (var batch in ExportCursor)
        {
            await connection.ExecuteAsync(batch);
        }
    }
}
