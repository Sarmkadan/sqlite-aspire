using Microsoft.Data.Sqlite;

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
    /// <exception cref="ArgumentNullException"><paramref name="destinationPath"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="destinationPath"/> is empty, white space, or resolves to the live database path.</exception>
    Task<SqliteBackupInfo> BackupAsync(string destinationPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the live database with the contents of a backup file. Requires exclusive access.
    /// </summary>
    /// <param name="sourcePath">Backup file to restore from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sourcePath"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="sourcePath"/> is empty, white space, or resolves to the live database path.</exception>
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
public sealed class SqliteBackupService : ISqliteBackupService
{
    private readonly SqliteOptions _options;

    /// <summary>
    /// Creates a backup service for the database described by <paramref name="options"/>.
    /// </summary>
    /// <param name="options">Options of the database to back up.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="options"/> has an invalid database path. See <see cref="SqliteOptions.Validate"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="options"/> has an out-of-range value. See <see cref="SqliteOptions.Validate"/>.</exception>
    public SqliteBackupService(SqliteOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        _options = options;
    }

    /// <inheritdoc />
    public Task<SqliteBackupInfo> BackupAsync(string destinationPath, CancellationToken cancellationToken = default)
    {
        // Validate on the caller's thread so bad arguments throw at call time, not inside the task.
        var fullPath = ValidatePath(destinationPath, nameof(destinationPath));
        return Task.Run(() => CreateBackup(fullPath, cancellationToken), cancellationToken);
    }

    private SqliteBackupInfo CreateBackup(string destinationPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = Path.GetFullPath(destinationPath);
        if (File.Exists(fullPath))
        {
            throw new IOException($"Backup destination already exists: '{fullPath}'.");
        }

        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using (var source = new SqliteConnection(_options.ToConnectionString()))
        using (var destination = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString()))
        {
            source.Open();
            destination.Open();

            cancellationToken.ThrowIfCancellationRequested();
            source.BackupDatabase(destination);
        }

        return new SqliteBackupInfo(fullPath, DateTimeOffset.UtcNow, new FileInfo(fullPath).Length);
    }

    /// <inheritdoc />
    public Task RestoreAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        // Validate on the caller's thread so bad arguments throw at call time, not inside the task.
        var fullPath = ValidatePath(sourcePath, nameof(sourcePath));
        return Task.Run(() => RestoreBackup(fullPath, cancellationToken), cancellationToken);
    }

    private string ValidatePath(string path, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, paramName);

        var fullPath = Path.GetFullPath(path);
        var livePath = Path.GetFullPath(_options.DatabasePath);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(fullPath, livePath, comparison))
        {
            throw new ArgumentException("Backup path must differ from the live database path");
        }

        return fullPath;
    }

    private void RestoreBackup(string sourcePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Backup file to restore from does not exist.", fullPath);
        }

        // Release pooled handles to the live file before overwriting its pages.
        SqliteConnection.ClearAllPools();

        using (var source = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString()))
        using (var destination = new SqliteConnection(_options.ToConnectionString()))
        {
            source.Open();
            destination.Open();

            cancellationToken.ThrowIfCancellationRequested();
            source.BackupDatabase(destination);

            // The backup copies the source's header, so pragmas must be re-applied to the restored file.
            SqlitePragmaInitializer.Apply(destination, _options);
        }

        // Closing the destination returned it to the pool; drop it so no handle to the live file remains.
        SqliteConnection.ClearAllPools();
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SqliteBackupInfo>> ListBackupsAsync(CancellationToken cancellationToken = default)
        => Task.Run<IReadOnlyList<SqliteBackupInfo>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var directory = _options.BackupDirectory;
            if (directory is null || !Directory.Exists(directory))
            {
                return [];
            }

            var backups = new List<SqliteBackupInfo>();
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var extension = Path.GetExtension(file);
                if (!extension.Equals(".db", StringComparison.OrdinalIgnoreCase)
                    && !extension.Equals(".bak", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var fullPath = Path.GetFullPath(file);
                backups.Add(new SqliteBackupInfo(fullPath, File.GetCreationTimeUtc(fullPath), new FileInfo(fullPath).Length));
            }

            backups.Sort((left, right) => right.CreatedAtUtc.CompareTo(left.CreatedAtUtc));
            return backups;
        }, cancellationToken);

    /// <inheritdoc />
    public async Task<int> PruneAsync(CancellationToken cancellationToken = default)
    {
        if (_options.BackupRetentionCount <= 0)
        {
            return 0;
        }

        var backups = await ListBackupsAsync(cancellationToken);
        var deleted = 0;
        foreach (var backup in backups.Skip(_options.BackupRetentionCount))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                File.Delete(backup.Path);
                deleted++;
            }
            catch (FileNotFoundException)
            {
                // Removed by someone else between listing and deletion.
            }
        }

        return deleted;
    }
}
