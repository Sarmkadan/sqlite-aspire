using Microsoft.Data.Sqlite;

namespace SqliteAspire.Tests;

public sealed class SqliteBackupServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"sqlite-aspire-backup-{Guid.NewGuid():N}");
    private readonly string _livePath;
    private readonly string _backupDirectory;
    private readonly SqliteOptions _options;
    private readonly SqliteBackupService _service;

    public SqliteBackupServiceTests()
    {
        _livePath = Path.Combine(_root, "live.db");
        _backupDirectory = Path.Combine(_root, "backups");
        Directory.CreateDirectory(_backupDirectory);

        _options = new SqliteOptions
        {
            DatabasePath = _livePath,
            BackupDirectory = _backupDirectory,
            Pooling = false,
        };
        _service = new SqliteBackupService(_options);

        using var connection = new SqliteConnection(_options.ToConnectionString());
        connection.Open();
        Execute(connection, "CREATE TABLE t(id INTEGER PRIMARY KEY, v TEXT)");
        Execute(connection, "INSERT INTO t(v) VALUES ('a'), ('b'), ('c')");
    }

    [Fact]
    public async Task BackupAsync_CreatesNonEmptyFileWithAllRows()
    {
        var destination = Path.Combine(_backupDirectory, "snapshot.db");

        var info = await _service.BackupAsync(destination);

        Assert.True(info.SizeBytes > 0);
        Assert.Equal(destination, info.Path);
        Assert.Equal(3, CountRows(destination));
    }

    [Fact]
    public async Task BackupAsync_ExistingDestination_ThrowsIOException()
    {
        var destination = Path.Combine(_backupDirectory, "existing.db");
        await File.WriteAllBytesAsync(destination, [1, 2, 3]);

        await Assert.ThrowsAsync<IOException>(() => _service.BackupAsync(destination));
    }

    [Fact]
    public async Task RestoreAsync_AfterExtraInserts_RestoresRowCount()
    {
        var backup = Path.Combine(_backupDirectory, "before.db");
        await _service.BackupAsync(backup);

        using (var connection = new SqliteConnection(_options.ToConnectionString()))
        {
            connection.Open();
            Execute(connection, "INSERT INTO t(v) VALUES ('d'), ('e')");
        }
        Assert.Equal(5, CountRows(_livePath));

        await _service.RestoreAsync(backup);

        Assert.Equal(3, CountRows(_livePath));
    }

    [Fact]
    public async Task RestoreAsync_MissingSource_ThrowsFileNotFoundException()
    {
        var missing = Path.Combine(_backupDirectory, "does-not-exist.db");

        await Assert.ThrowsAsync<FileNotFoundException>(() => _service.RestoreAsync(missing));
    }

    [Fact]
    public async Task BackupAsync_WhileAnotherConnectionHoldsReadTransaction_Succeeds()
    {
        var destination = Path.Combine(_backupDirectory, "during-read.db");

        using var reader = new SqliteConnection(_options.ToConnectionString());
        reader.Open();
        using var transaction = reader.BeginTransaction();
        using (var select = reader.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText = "SELECT COUNT(*) FROM t";
            Assert.Equal(3L, select.ExecuteScalar());
        }

        var info = await _service.BackupAsync(destination);

        Assert.True(info.SizeBytes > 0);
        Assert.Equal(3, CountRows(destination));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static int CountRows(string databasePath)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString());
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM t";
        return Convert.ToInt32(command.ExecuteScalar());
    }
}
