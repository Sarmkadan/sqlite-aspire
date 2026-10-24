using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace SqliteAspire.Tests;

public sealed class SqliteResourceBuilderExtensionsTests : IDisposable
{
    private readonly string _customDirectory;
    private readonly bool _customDirectoryPreexisted;

    public SqliteResourceBuilderExtensionsTests()
    {
        // AddSqlite creates the directory of a relative custom path under the app host directory.
        // Remember whether it existed so Dispose can remove only what the tests created.
        _customDirectory = Path.Combine(DistributedApplication.CreateBuilder().AppHostDirectory, "custom");
        _customDirectoryPreexisted = Directory.Exists(_customDirectory);
    }

    public void Dispose()
    {
        if (!_customDirectoryPreexisted && Directory.Exists(_customDirectory))
        {
            Directory.Delete(_customDirectory, recursive: true);
        }
    }

    [Fact]
    public void AddSqlite_DefaultPath_RegistersResourceNamedAfterDatabase()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddSqlite("orders");

        var resource = Assert.Single(builder.Resources.OfType<SqliteResource>());
        Assert.Equal("orders", resource.Name);
        Assert.EndsWith("orders.db", resource.DatabasePath);
        Assert.True(Path.IsPathFullyQualified(resource.DatabasePath));
    }

    [Fact]
    public void AddSqlite_RelativeCustomPath_ResolvesToAbsolutePath()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddSqlite("x", Path.Combine("custom", "path.db"));

        var resource = Assert.Single(builder.Resources.OfType<SqliteResource>());
        Assert.True(Path.IsPathFullyQualified(resource.DatabasePath));
        Assert.EndsWith(Path.Combine("custom", "path.db"), resource.DatabasePath);
    }

    [Fact]
    public void WithWalMode_False_DisablesWalMode()
    {
        var builder = DistributedApplication.CreateBuilder();
        var sqlite = builder.AddSqlite("orders");

        sqlite.WithWalMode(false);

        Assert.False(sqlite.Resource.Options.WalMode);
    }

    [Fact]
    public void WithConnectionPooling_DisabledWithBusyTimeout_SetsBothOptions()
    {
        var builder = DistributedApplication.CreateBuilder();
        var sqlite = builder.AddSqlite("orders");

        sqlite.WithConnectionPooling(false, TimeSpan.FromSeconds(9));

        Assert.False(sqlite.Resource.Options.Pooling);
        Assert.Equal(TimeSpan.FromSeconds(9), sqlite.Resource.Options.BusyTimeout);
    }

    [Fact]
    public void WithBackup_SetsDirectoryIntervalAndRetention()
    {
        var builder = DistributedApplication.CreateBuilder();
        var sqlite = builder.AddSqlite("orders");

        sqlite.WithBackup("bk", TimeSpan.FromMinutes(5), 3);

        var options = sqlite.Resource.Options;
        Assert.NotNull(options.BackupDirectory);
        Assert.True(Path.IsPathFullyQualified(options.BackupDirectory));
        Assert.EndsWith("bk", options.BackupDirectory);
        Assert.Equal(TimeSpan.FromMinutes(5), options.BackupInterval);
        Assert.Equal(3, options.BackupRetentionCount);
    }

    [Fact]
    public async Task ConnectionStringExpression_GetValueAsync_ReturnsSqliteConnectionString()
    {
        var builder = DistributedApplication.CreateBuilder();
        var sqlite = builder.AddSqlite("orders");

        var connectionString = await sqlite.Resource.ConnectionStringExpression.GetValueAsync(CancellationToken.None);

        Assert.NotNull(connectionString);
        Assert.Contains("Data Source=", connectionString);
    }
}
