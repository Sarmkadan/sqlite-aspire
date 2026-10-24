namespace SqliteAspire.Tests;

public sealed class SqliteBackupServicePruneTests : IDisposable
{
    private static readonly DateTime BaseTimeUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"sqlite-aspire-prune-{Guid.NewGuid():N}");
    private readonly string _backupDirectory;

    public SqliteBackupServicePruneTests()
    {
        _backupDirectory = Path.Combine(_root, "backups");
        Directory.CreateDirectory(_backupDirectory);

        // backup-4.db is the newest (BaseTimeUtc + 4 minutes), backup-0.db the oldest.
        for (var i = 0; i < 5; i++)
        {
            var path = Path.Combine(_backupDirectory, $"backup-{i}.db");
            File.WriteAllText(path, "dummy");
            File.SetCreationTimeUtc(path, BaseTimeUtc.AddMinutes(i));
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task ListBackupsAsync_ReturnsAllBackupsNewestFirst()
    {
        var service = CreateService(_backupDirectory, retentionCount: 24);

        var backups = await service.ListBackupsAsync();

        Assert.Equal(
            ["backup-4.db", "backup-3.db", "backup-2.db", "backup-1.db", "backup-0.db"],
            backups.Select(b => Path.GetFileName(b.Path)));
        Assert.Equal(backups.OrderByDescending(b => b.CreatedAtUtc), backups);
    }

    [Fact]
    public async Task ListBackupsAsync_EmptyDirectory_ReturnsEmptyList()
    {
        var emptyDirectory = Path.Combine(_root, "empty");
        Directory.CreateDirectory(emptyDirectory);
        var service = CreateService(emptyDirectory, retentionCount: 2);

        var backups = await service.ListBackupsAsync();

        Assert.Empty(backups);
    }

    [Fact]
    public async Task ListBackupsAsync_MissingDirectory_ReturnsEmptyList()
    {
        var missingDirectory = Path.Combine(_root, "does-not-exist");
        var service = CreateService(missingDirectory, retentionCount: 2);

        var backups = await service.ListBackupsAsync();

        Assert.Empty(backups);
    }

    [Fact]
    public async Task PruneAsync_RetentionTwo_DeletesThreeOldestAndKeepsTwoNewest()
    {
        var service = CreateService(_backupDirectory, retentionCount: 2);

        var deleted = await service.PruneAsync();

        Assert.Equal(3, deleted);
        Assert.Equal(
            ["backup-3.db", "backup-4.db"],
            Directory.GetFiles(_backupDirectory).Select(Path.GetFileName).Order());
    }

    [Fact]
    public async Task PruneAsync_RetentionAboveFileCount_DeletesNothing()
    {
        var service = CreateService(_backupDirectory, retentionCount: 10);

        var deleted = await service.PruneAsync();

        Assert.Equal(0, deleted);
        Assert.Equal(5, Directory.GetFiles(_backupDirectory).Length);
    }

    [Fact]
    public async Task PruneAsync_NullBackupDirectory_ListsNothingAndDeletesNothing()
    {
        var service = CreateService(backupDirectory: null, retentionCount: 2);

        var backups = await service.ListBackupsAsync();
        var deleted = await service.PruneAsync();

        Assert.Empty(backups);
        Assert.Equal(0, deleted);
        Assert.Equal(5, Directory.GetFiles(_backupDirectory).Length);
    }

    private SqliteBackupService CreateService(string? backupDirectory, int retentionCount)
        => new(new SqliteOptions
        {
            DatabasePath = Path.Combine(_root, "live.db"),
            BackupDirectory = backupDirectory,
            BackupRetentionCount = retentionCount,
            Pooling = false,
        });
}
