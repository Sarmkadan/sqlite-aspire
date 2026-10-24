using System.Data;
using Microsoft.Data.Sqlite;

namespace SqliteAspire;

/// <summary>
/// Applies the connection-level PRAGMAs described by <see cref="SqliteOptions"/> to a <see cref="SqliteConnection"/>:
/// journal mode, synchronous level and busy timeout. Safe to call repeatedly on the same connection.
/// </summary>
public static class SqlitePragmaInitializer
{
    private const string MemoryDataSource = ":memory:";

    /// <summary>
    /// Opens <paramref name="connection"/> if it is closed, then executes the journal mode, synchronous and busy timeout PRAGMAs.
    /// </summary>
    /// <param name="connection">The connection to configure. It is opened if it is currently closed.</param>
    /// <param name="options">Options that supply the journal mode, synchronous level and busy timeout.</param>
    /// <remarks>
    /// <c>PRAGMA journal_mode</c> is skipped for in-memory databases because WAL is not supported there.
    /// Re-applying the same values is a no-op on the database, so the method can be called on every open.
    /// </remarks>
    public static void Apply(SqliteConnection connection, SqliteOptions options)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(options);

        if (connection.State == ConnectionState.Closed)
        {
            connection.Open();
        }

        if (!IsInMemory(connection, options))
        {
            var journalMode = options.WalMode ? "WAL" : "DELETE";
            Execute(connection, $"PRAGMA journal_mode={journalMode}");
        }

        Execute(connection, $"PRAGMA synchronous={ToSynchronousKeyword(options.Synchronous)}");
        Execute(connection, $"PRAGMA busy_timeout={(long)options.BusyTimeout.TotalMilliseconds}");
    }

    private static bool IsInMemory(SqliteConnection connection, SqliteOptions options)
        => options.DatabasePath == MemoryDataSource || connection.DataSource == MemoryDataSource;

    private static string ToSynchronousKeyword(SqliteSynchronous synchronous) => synchronous switch
    {
        SqliteSynchronous.Off => "OFF",
        SqliteSynchronous.Normal => "NORMAL",
        SqliteSynchronous.Full => "FULL",
        _ => throw new ArgumentOutOfRangeException(nameof(synchronous), synchronous, "Unknown synchronous level."),
    };

    private static void Execute(SqliteConnection connection, string commandText)
    {
        using var command = connection.CreateCommand();
        command.CommandText = commandText;
        command.ExecuteNonQuery();
    }
}
