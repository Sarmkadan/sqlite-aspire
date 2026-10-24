using Microsoft.Data.Sqlite;

namespace SqliteAspire;

/// <summary>
/// Configuration for a <see cref="SqliteResource"/>: journal mode, pooling, busy timeout and backup policy.
/// </summary>
/// <remarks>
/// At runtime, <c>AddSqliteClient</c> binds per-connection overrides from <c>Sqlite:{connectionName}</c>.
/// The appsettings shape is:
/// <code language="json">
/// {
///   "Sqlite": {
///     "orders": {
///       "WalMode": true,
///       "Synchronous": "Normal",
///       "Pooling": true,
///       "BusyTimeout": "00:00:05",
///       "BackupDirectory": "/var/backups/orders",
///       "BackupInterval": "01:00:00",
///       "BackupRetentionCount": 24,
///       "HealthCheck": true
///     }
///   }
/// }
/// </code>
/// Keys are optional and match the property names. <c>DatabasePath</c> and <c>Pooling</c> may also be set here, but they
/// are normally taken from <c>ConnectionStrings:{connectionName}</c>; values in this section override the connection string.
/// </remarks>
public sealed class SqliteOptions
{
    /// <summary>Configuration section name. Per-connection overrides live under <c>Sqlite:{connectionName}</c>.</summary>
    public const string SectionName = "Sqlite";

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
    /// Checks that the options describe a usable configuration. Backup settings are only checked when
    /// <see cref="BackupDirectory"/> is set.
    /// </summary>
    /// <exception cref="ArgumentException"><see cref="DatabasePath"/> is <see langword="null"/>, empty or white space.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see cref="BusyTimeout"/> is negative; or <see cref="BackupDirectory"/> is set and <see cref="BackupInterval"/> is
    /// zero or negative, or <see cref="BackupRetentionCount"/> is less than 1; or <see cref="Synchronous"/> is not a defined value.
    /// </exception>
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(DatabasePath, nameof(DatabasePath));
        ArgumentOutOfRangeException.ThrowIfLessThan(BusyTimeout, TimeSpan.Zero, nameof(BusyTimeout));

        if (BackupDirectory is not null)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(BackupInterval, TimeSpan.Zero, nameof(BackupInterval));
            ArgumentOutOfRangeException.ThrowIfLessThan(BackupRetentionCount, 1, nameof(BackupRetentionCount));
        }

        if (!Enum.IsDefined(Synchronous))
        {
            throw new ArgumentOutOfRangeException(nameof(Synchronous), Synchronous, "Value is not a defined SqliteSynchronous member.");
        }
    }

    /// <summary>
    /// Builds a Microsoft.Data.Sqlite connection string from the current options.
    /// </summary>
    /// <returns>A connection string suitable for <c>SqliteConnection</c>.</returns>
    /// <exception cref="ArgumentException">The options are invalid. See <see cref="Validate"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The options are invalid. See <see cref="Validate"/>.</exception>
    public string ToConnectionString()
    {
        Validate();

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
