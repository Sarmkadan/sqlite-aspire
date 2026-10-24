using Microsoft.Data.Sqlite;

namespace SqliteAspire;

/// <summary>
/// Configuration for a <see cref="SqliteResource"/>: journal mode, pooling, busy timeout and backup policy.
/// </summary>
public sealed class SqliteOptions
{
    /// <summary>Absolute or app-relative path to the database file. <c>":memory:"</c> for an in-memory database.</summary>
    public string DatabasePath { get; set; } = "app.db";

    /// <summary>Enables <c>PRAGMA journal_mode=WAL</c> on first open. Default: <see langword="true"/>.</summary>
    public bool WalMode { get; set; } = true;

    /// <summary><c>PRAGMA synchronous</c> level. <c>NORMAL</c> is the recommended pairing with WAL.</summary>
    public SqliteSynchronous Synchronous { get; set; } = SqliteSynchronous.Normal;

    /// <summary>Enables ADO.NET connection pooling (<c>Pooling=True</c> in the connection string). Default: <see langword="true"/>.</summary>
    public bool Pooling { get; set; } = true;

    /// <summary>Time a writer waits on a locked database before failing with SQLITE_BUSY.</summary>
    public TimeSpan BusyTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Directory for periodic backups. <see langword="null"/> disables scheduled backups.</summary>
    public string? BackupDirectory { get; set; }

    /// <summary>Interval between scheduled backups. Ignored when <see cref="BackupDirectory"/> is <see langword="null"/>.</summary>
    public TimeSpan BackupInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Number of most recent backup files to keep. Older files are deleted after each successful backup.</summary>
    public int BackupRetentionCount { get; set; } = 24;

    /// <summary>Registers a health check that opens a connection and runs <c>PRAGMA quick_check</c>.</summary>
    public bool HealthCheck { get; set; } = true;

    /// <summary>
    /// Builds a Microsoft.Data.Sqlite connection string from the current options.
    /// </summary>
    /// <returns>A connection string suitable for <c>SqliteConnection</c>.</returns>
    public string ToConnectionString()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Pooling = Pooling,
            DefaultTimeout = (int)BusyTimeout.TotalSeconds,
            Mode = DatabasePath == ":memory:" ? SqliteOpenMode.Memory : SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Default,
        };

        return builder.ToString();
    }
}

/// <summary>Values for <c>PRAGMA synchronous</c>.</summary>
public enum SqliteSynchronous
{
    /// <summary>No fsync. Fastest, unsafe on power loss.</summary>
    Off = 0,

    /// <summary>Fsync at WAL checkpoints. Safe with WAL, may lose last transactions on power loss.</summary>
    Normal = 1,

    /// <summary>Fsync on every commit. Slowest, durable.</summary>
    Full = 2,
}
