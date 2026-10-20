using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace SqliteAspire;

/// <summary>
/// Fluent extensions for adding and configuring a <see cref="SqliteResource"/> in an Aspire app host.
/// </summary>
public static class SqliteResourceBuilderExtensions
{
    /// <summary>
    /// Adds a SQLite database resource to the distributed application.
    /// </summary>
    /// <param name="builder">The app host builder.</param>
    /// <param name="name">Resource name; also the connection string key for dependents.</param>
    /// <param name="databasePath">Path to the database file. Defaults to <c>{name}.db</c> in the app host directory.</param>
    /// <returns>A resource builder for further configuration.</returns>
    public static IResourceBuilder<SqliteResource> AddSqlite(
        this IDistributedApplicationBuilder builder,
        string name,
        string? databasePath = null)
        => throw new NotImplementedException();

    /// <summary>Enables or disables WAL journal mode.</summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="enabled">Whether WAL is enabled. Default: <see langword="true"/>.</param>
    public static IResourceBuilder<SqliteResource> WithWalMode(
        this IResourceBuilder<SqliteResource> builder,
        bool enabled = true)
        => throw new NotImplementedException();

    /// <summary>Configures ADO.NET connection pooling and busy timeout.</summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="enabled">Whether pooling is enabled.</param>
    /// <param name="busyTimeout">Writer wait time on a locked database. <see langword="null"/> keeps the default.</param>
    public static IResourceBuilder<SqliteResource> WithConnectionPooling(
        this IResourceBuilder<SqliteResource> builder,
        bool enabled = true,
        TimeSpan? busyTimeout = null)
    {
        builder.Resource.Options.Pooling = enabled;

        if (busyTimeout.HasValue)
        {
            builder.Resource.Options.BusyTimeout = busyTimeout.Value;
        }

        return builder;
    }

    /// <summary>Schedules periodic online backups via the SQLite backup API.</summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="backupDirectory">Directory to write backup files to.</param>
    /// <param name="interval">Interval between backups. Default: 1 hour.</param>
    /// <param name="retentionCount">Number of most recent backups to keep. Default: 24.</param>
    public static IResourceBuilder<SqliteResource> WithBackup(
        this IResourceBuilder<SqliteResource> builder,
        string backupDirectory,
        TimeSpan? interval = null,
        int retentionCount = 24)
        => throw new NotImplementedException();

    /// <summary>Registers a health check that opens a connection and runs <c>PRAGMA quick_check</c>.</summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="enabled">Whether the health check is registered. Default: <see langword="true"/>.</param>
    public static IResourceBuilder<SqliteResource> WithHealthCheck(
        this IResourceBuilder<SqliteResource> builder,
        bool enabled = true)
        => throw new NotImplementedException();
}
