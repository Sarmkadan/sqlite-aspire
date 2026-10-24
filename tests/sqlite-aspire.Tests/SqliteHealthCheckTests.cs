using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SqliteAspire.Tests;

public sealed class SqliteHealthCheckTests : IDisposable
{
    private readonly TempDatabaseFile _database = new();

    [Fact]
    public async Task CheckHealthAsync_ValidWalDatabase_ReturnsHealthyWithWalJournalMode()
    {
        var options = CreateOptions(_database.Path, walMode: true);
        using (var connection = new SqliteConnection(options.ToConnectionString()))
        {
            SqlitePragmaInitializer.Apply(connection, options);
        }

        var check = new SqliteHealthCheck(options);
        var result = await check.CheckHealthAsync(CreateContext(check));

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("wal", result.Data["journal_mode"]);
    }

    [Fact]
    public async Task CheckHealthAsync_DeleteModeFileWhenWalExpected_ReturnsDegraded()
    {
        var setup = CreateOptions(_database.Path, walMode: false);
        using (var connection = new SqliteConnection(setup.ToConnectionString()))
        {
            SqlitePragmaInitializer.Apply(connection, setup);
        }

        var options = CreateOptions(_database.Path, walMode: true);
        var check = new SqliteHealthCheck(options);
        var result = await check.CheckHealthAsync(CreateContext(check));

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal("delete", result.Data["journal_mode"]);
    }

    [Fact]
    public async Task CheckHealthAsync_GarbageFile_ReturnsUnhealthy()
    {
        var bytes = new byte[4096];
        RandomNumberGenerator.Fill(bytes);
        await File.WriteAllBytesAsync(_database.Path, bytes);

        var options = CreateOptions(_database.Path, walMode: true);
        var check = new SqliteHealthCheck(options);
        var result = await check.CheckHealthAsync(CreateContext(check));

        Assert.NotEqual(HealthStatus.Healthy, result.Status);
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_NonExistentDirectory_ReturnsUnhealthy()
    {
        var missingDirectory = Path.Combine(Path.GetTempPath(), $"sqlite-aspire-missing-{Guid.NewGuid():N}");
        var options = CreateOptions(Path.Combine(missingDirectory, "app.db"), walMode: true);
        var check = new SqliteHealthCheck(options);

        var result = await check.CheckHealthAsync(CreateContext(check));

        Assert.NotEqual(HealthStatus.Healthy, result.Status);
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_CancelledToken_ThrowsOrReturnsUnhealthy()
    {
        var options = CreateOptions(_database.Path, walMode: true);
        using (var connection = new SqliteConnection(options.ToConnectionString()))
        {
            SqlitePragmaInitializer.Apply(connection, options);
        }

        var check = new SqliteHealthCheck(options);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        HealthCheckResult? result = null;
        var exception = await Record.ExceptionAsync(
            async () => result = await check.CheckHealthAsync(CreateContext(check), cts.Token));

        if (exception is null)
        {
            Assert.Equal(HealthStatus.Unhealthy, result!.Value.Status);
        }
        else
        {
            Assert.IsAssignableFrom<OperationCanceledException>(exception);
        }
    }

    public void Dispose() => _database.Dispose();

    private static SqliteOptions CreateOptions(string databasePath, bool walMode) => new()
    {
        DatabasePath = databasePath,
        WalMode = walMode,
        Pooling = false,
    };

    private static HealthCheckContext CreateContext(IHealthCheck check) => new()
    {
        Registration = new HealthCheckRegistration("t", check, null, null),
    };

    /// <summary>
    /// A database file under the temp directory with a unique name. Deletes the file and its -wal/-shm companions on dispose.
    /// </summary>
    private sealed class TempDatabaseFile : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"sqlite-aspire-health-{Guid.NewGuid():N}.db");

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
