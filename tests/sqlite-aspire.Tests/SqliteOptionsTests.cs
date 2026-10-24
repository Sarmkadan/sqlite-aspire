using Microsoft.Data.Sqlite;

namespace SqliteAspire.Tests;

public class SqliteOptionsTests
{
    [Fact]
    public void ToConnectionString_Defaults_ParseToExpectedValues()
    {
        var connectionString = new SqliteOptions().ToConnectionString();

        var builder = new SqliteConnectionStringBuilder(connectionString);

        Assert.Equal("app.db", builder.DataSource);
        Assert.True(builder.Pooling);
        Assert.Equal(5, builder.DefaultTimeout);
    }

    [Fact]
    public void ToConnectionString_PoolingDisabled_WritesPoolingFalse()
    {
        var options = new SqliteOptions { Pooling = false };

        var connectionString = options.ToConnectionString();

        Assert.Contains("Pooling=False", connectionString);
        Assert.False(new SqliteConnectionStringBuilder(connectionString).Pooling);
    }

    [Fact]
    public void ToConnectionString_BusyTimeout30Seconds_SetsDefaultTimeout()
    {
        var options = new SqliteOptions { BusyTimeout = TimeSpan.FromSeconds(30) };

        var builder = new SqliteConnectionStringBuilder(options.ToConnectionString());

        Assert.Equal(30, builder.DefaultTimeout);
    }

    [Fact]
    public void ToConnectionString_InMemoryPath_SetsMemoryMode()
    {
        var options = new SqliteOptions { DatabasePath = ":memory:" };

        var builder = new SqliteConnectionStringBuilder(options.ToConnectionString());

        Assert.Equal(SqliteOpenMode.Memory, builder.Mode);
        Assert.Equal(":memory:", builder.DataSource);
    }

    [Fact]
    public void ToConnectionString_PathWithSpaces_RoundTrips()
    {
        const string path = "/var/lib/my app/order data.db";
        var options = new SqliteOptions { DatabasePath = path };

        var builder = new SqliteConnectionStringBuilder(options.ToConnectionString());

        Assert.Equal(path, builder.DataSource);
    }
}
