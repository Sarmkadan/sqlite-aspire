using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace SqliteAspire.Tests;

public sealed class SqliteClientExtensionsTests : IDisposable
{
    private readonly TempDatabaseFile _database = new();

    [Fact]
    public void AddSqliteClient_ResolvesOptionsFromConnectionString()
    {
        var builder = CreateBuilder(ConnectionString(_database.Path));
        builder.AddSqliteClient("orders");

        using var host = builder.Build();
        var options = host.Services.GetRequiredService<SqliteOptions>();

        Assert.Equal(_database.Path, options.DatabasePath);
        Assert.False(options.Pooling);
    }

    [Fact]
    public void AddSqliteClient_ResolvesBackupServiceImplementation()
    {
        var builder = CreateBuilder(ConnectionString(_database.Path));
        builder.AddSqliteClient("orders");

        using var host = builder.Build();
        var service = host.Services.GetRequiredService<ISqliteBackupService>();

        Assert.IsType<SqliteBackupService>(service);
    }

    [Fact]
    public async Task AddSqliteClient_RegistersHealthCheckNamedAfterConnection()
    {
        var builder = CreateBuilder(ConnectionString(_database.Path));
        builder.AddSqliteClient("orders");

        using var host = builder.Build();
        var healthCheckService = host.Services.GetRequiredService<HealthCheckService>();
        var report = await healthCheckService.CheckHealthAsync();

        Assert.True(report.Entries.ContainsKey("sqlite-orders"));
        // A freshly created file is in rollback-journal mode, so the check may be Degraded for WAL expectations.
        // Unhealthy would mean the database could not be opened at all.
        Assert.NotEqual(HealthStatus.Unhealthy, report.Entries["sqlite-orders"].Status);
    }

    [Fact]
    public void AddSqliteClient_MissingConnectionString_ThrowsNamingTheConnection()
    {
        var builder = CreateBuilder(connectionString: null);

        var ex = Assert.Throws<InvalidOperationException>(() => builder.AddSqliteClient("orders"));

        Assert.Contains("orders", ex.Message);
    }

    [Fact]
    public void AddSqliteClient_ConfigureSetsBackupDirectory_RegistersBackupScheduler()
    {
        var backupDirectory = Path.Combine(Path.GetTempPath(), $"sqlite-aspire-backups-{Guid.NewGuid():N}");
        var builder = CreateBuilder(ConnectionString(_database.Path));
        builder.AddSqliteClient("orders", options => options.BackupDirectory = backupDirectory);

        using var host = builder.Build();
        var hostedServices = host.Services.GetServices<IHostedService>();

        Assert.Contains(hostedServices, service => service is SqliteBackupScheduler);
    }

    private static HostApplicationBuilder CreateBuilder(string? connectionString)
    {
        var builder = Host.CreateApplicationBuilder();
        if (connectionString is not null)
        {
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:orders"] = connectionString,
            });
        }

        return builder;
    }

    private static string ConnectionString(string path) => $"Data Source={path};Pooling=False";

    public void Dispose() => _database.Dispose();

    /// <summary>
    /// A database file under the temp directory with a unique name. Deletes the file and its -wal/-shm companions on dispose.
    /// </summary>
    private sealed class TempDatabaseFile : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"sqlite-aspire-client-{Guid.NewGuid():N}.db");

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
