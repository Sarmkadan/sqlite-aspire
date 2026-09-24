namespace SqliteAspire;

/// <summary>
/// Online backup and restore for a SQLite database using the native backup API
/// (<c>VACUUM INTO</c> / <c>sqlite3_backup</c>), safe to run while writers are active.
/// </summary>
public interface ISqliteBackupService
{
    /// <summary>
    /// Creates a consistent snapshot of the database.
    /// </summary>
    /// <param name="destinationPath">Target file path. Must not exist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Metadata describing the created backup.</returns>
    Task<SqliteBackupInfo> BackupAsync(string destinationPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the live database with the contents of a backup file. Requires exclusive access.
    /// </summary>
    /// <param name="sourcePath">Backup file to restore from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RestoreAsync(string sourcePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists backups in the configured backup directory, newest first.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<SqliteBackupInfo>> ListBackupsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes backups beyond <see cref="SqliteOptions.BackupRetentionCount"/>.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of files deleted.</returns>
    Task<int> PruneAsync(CancellationToken cancellationToken = default);
}

/// <summary>Metadata about a single backup file.</summary>
/// <param name="Path">Absolute path of the backup file.</param>
/// <param name="CreatedAtUtc">Creation time in UTC.</param>
/// <param name="SizeBytes">File size in bytes.</param>
public sealed record SqliteBackupInfo(string Path, DateTimeOffset CreatedAtUtc, long SizeBytes);

/// <summary>
/// Default <see cref="ISqliteBackupService"/> backed by Microsoft.Data.Sqlite.
/// </summary>
/// <param name="options">Options of the database to back up.</param>
public sealed class SqliteBackupService(SqliteOptions options) : ISqliteBackupService
{
    private readonly SqliteOptions _options = options;

    /// <inheritdoc />
    public Task<SqliteBackupInfo> BackupAsync(string destinationPath, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <inheritdoc />
    public Task RestoreAsync(string sourcePath, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<IReadOnlyList<SqliteBackupInfo>> ListBackupsAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<int> PruneAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
