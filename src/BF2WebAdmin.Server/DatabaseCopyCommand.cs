using BF2WebAdmin.Data;
using BF2WebAdmin.Data.Copy;

namespace BF2WebAdmin.Server;

/// <summary>
/// BF2WebAdmin.Server --db-copy-from "&lt;SQL Server connection string&gt;": copies WebAdmin's SQL Server database into the
/// configured Postgres database (ConnectionStrings:BF2DB with Database:Provider postgres), which must be empty, and
/// exits. Without a value after the flag the source is ConnectionStrings:CopySource (env ConnectionStrings__CopySource),
/// which keeps the password off the command line. docs/database.md has the switch-over runbook.
/// </summary>
public static class DatabaseCopyCommand
{
    public const string Flag = "--db-copy-from";

    public static async Task<int> RunAsync(string[] args, IConfiguration configuration)
    {
        try
        {
            var index = Array.IndexOf(args, Flag);
            var source = index + 1 < args.Length && !args[index + 1].StartsWith("--") ? args[index + 1] : configuration.GetConnectionString("CopySource");
            if (string.IsNullOrWhiteSpace(source))
            {
                Console.Error.WriteLine($"{Flag} needs the SQL Server connection string of the source, after the flag or in ConnectionStrings:CopySource");
                return 2;
            }

            if (DatabaseProviders.IsNpgsqlConnectionString(source) || source.Contains(".sqlite", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine($"The source of {Flag} must be a SQL Server connection string");
                return 2;
            }

            var target = DatabaseSetup.FromConfiguration(configuration);
            if (target.Provider != DatabaseProvider.Postgres)
            {
                Console.Error.WriteLine($"The target (ConnectionStrings:BF2DB) is {target.Provider}; the copy goes into Postgres only. " +
                                        $"Set ConnectionStrings__BF2DB to the new Postgres database (and Database__Provider=postgres).");
                return 2;
            }

            var copier = new SqlServerToPostgresCopier(source, target.ConnectionString, Console.Out);
            var result = await copier.CopyAsync();
            SqlServerToPostgresCopier.WriteSummary(result, Console.Out);
            return result.Committed ? 0 : 1;
        }
        catch (DatabaseCopyException ex)
        {
            Console.Error.WriteLine("Copy refused: " + ex.Message);
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Copy FAILED, nothing was committed: " + ex);
            return 1;
        }
    }
}
