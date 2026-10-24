using Microsoft.Data.Sqlite;

namespace SqliteAspire.Tests;

public sealed class SqlitePragmaInitializerTests : IDisposable
{
    private readonly TempDatabaseFile _database = new();

    [Fact]
    public void Apply_WalModeTrue_SetsJournalModeToWal()
    {
        var options = CreateOptions(_database.Path, walMode: true);
        using var connection = new SqliteConnection(options.ToConnectionString());

        SqlitePragmaInitializer.Apply(connection, options);

        Assert.Equal("wal", ReadPragma(connection, "journal_mode"));
    }

    [Fact]
    public void Apply_WalModeFalse_SetsJournalModeToDelete()
    {
        var options = CreateOptions(_database.Path, walMode: false);
        using var connection = new SqliteConnection(options.ToConnectionString());

        SqlitePragmaInitializer.Apply(connection, options);

        Assert.Equal("delete", ReadPragma(connection, "journal_mode"));
    }

    [Theory]
    [InlineData(SqliteSynchronous.Off, "0")]
    [InlineData(SqliteSynchronous.Full, "2")]
    public void Apply_Synchronous_SetsSynchronousLevel(SqliteSynchronous synchronous, string expected)
    {
        var options = CreateOptions(_database.Path, walMode: true);
        options.Synchronous = synchronous;
        using var connection = new SqliteConnection(options.ToConnectionString());

        SqlitePragmaInitializer.Apply(connection, options);

        Assert.Equal(expected, ReadPragma(connection, "synchronous"));
    }

    [Fact]
    public void Apply_CalledTwice_DoesNotThrow()
    {
        var options = CreateOptions(_database.Path, walMode: true);
        using var connection = new SqliteConnection(options.ToConnectionString());

        SqlitePragmaInitializer.Apply(connection, options);
        var exception = Record.Exception(() => SqlitePragmaInitializer.Apply(connection, options));

        Assert.Null(exception);
        Assert.Equal("wal", ReadPragma(connection, "journal_mode"));
    }

    [Fact]
    public void Apply_InMemoryDatabaseWithWalMode_DoesNotThrow()
    {
        var options = CreateOptions(":memory:", walMode: true);
        using var connection = new SqliteConnection(options.ToConnectionString());

        var exception = Record.Exception(() => SqlitePragmaInitializer.Apply(connection, options));

        Assert.Null(exception);
    }

    public void Dispose() => _database.Dispose();

    private static SqliteOptions CreateOptions(string databasePath, bool walMode) => new()
    {
        DatabasePath = databasePath,
        WalMode = walMode,
        Pooling = false,
    };

    private static string? ReadPragma(SqliteConnection connection, string pragmaName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA {pragmaName}";
        return command.ExecuteScalar()?.ToString();
    }

    /// <summary>
    /// A database file under the temp directory with a unique name. Deletes the file and its -wal/-shm companions on dispose.
    /// </summary>
    private sealed class TempDatabaseFile : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"sqlite-aspire-pragma-{Guid.NewGuid():N}.db");

        public void Dispose()
        {
            foreach (var file in new[] { Path, Path + "-wal", Path + "-shm" })
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }
    }
}
