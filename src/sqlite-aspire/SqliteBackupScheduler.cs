using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SqliteAspire;

/// <summary>
/// Hosted service that takes a backup of the database every <see cref="SqliteOptions.BackupInterval"/>
/// and prunes old backups down to <see cref="SqliteOptions.BackupRetentionCount"/>.
/// Does nothing when <see cref="SqliteOptions.BackupDirectory"/> is <see langword="null"/>.
/// </summary>
/// <param name="backupService">Service used to create and prune backup files.</param>
/// <param name="options">Options of the database to back up.</param>
/// <param name="logger">Logger for backup outcomes.</param>
public sealed class SqliteBackupScheduler(
    ISqliteBackupService backupService,
    SqliteOptions options,
    ILogger<SqliteBackupScheduler> logger) : BackgroundService
{
    private readonly ISqliteBackupService _backupService = backupService;
    private readonly SqliteOptions _options = options;
    private readonly ILogger<SqliteBackupScheduler> _logger = logger;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var backupDirectory = _options.BackupDirectory;
        if (backupDirectory is null)
        {
            return;
        }

        using var timer = new PeriodicTimer(_options.BackupInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunBackupAsync(backupDirectory, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host is shutting down; leave the loop without logging an error.
        }
    }

    private async Task RunBackupAsync(string backupDirectory, CancellationToken cancellationToken)
    {
        try
        {
            var fileName = $"{Path.GetFileNameWithoutExtension(_options.DatabasePath)}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.db";
            var destinationPath = Path.Combine(backupDirectory, fileName);

            var backup = await _backupService.BackupAsync(destinationPath, cancellationToken);
            await _backupService.PruneAsync(cancellationToken);

            _logger.LogInformation("Created scheduled backup {BackupPath} ({SizeBytes} bytes)", backup.Path, backup.SizeBytes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Keep the loop alive; the next tick retries.
            _logger.LogError(ex, "Scheduled backup of '{DatabasePath}' failed", _options.DatabasePath);
        }
    }
}
