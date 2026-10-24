using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace SqliteAspire;

/// <summary>
/// Registers SQLite services in an application that receives its connection string from configuration
/// (the runtime side of a <see cref="SqliteResource"/>).
/// </summary>
public static class SqliteClientExtensions
{
    /// <summary>
    /// Reads <c>ConnectionStrings:{connectionName}</c> and registers <see cref="SqliteOptions"/>,
    /// <see cref="ISqliteBackupService"/>, the health check and the backup scheduler according to those options.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="connectionName">Name of the connection string, as used by the app host resource.</param>
    /// <param name="configure">Optional callback to adjust the options parsed from the connection string.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="InvalidOperationException">The connection string is missing or empty.</exception>
    public static IHostApplicationBuilder AddSqliteClient(
        this IHostApplicationBuilder builder,
        string connectionName,
        Action<SqliteOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);

        var connectionString = builder.Configuration.GetConnectionString(connectionName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{connectionName}' is missing. Set 'ConnectionStrings:{connectionName}' in configuration.");
        }

        var connectionStringBuilder = new SqliteConnectionStringBuilder(connectionString);
        var options = new SqliteOptions
        {
            DatabasePath = connectionStringBuilder.DataSource,
            Pooling = connectionStringBuilder.Pooling,
            BusyTimeout = TimeSpan.FromSeconds(connectionStringBuilder.DefaultTimeout),
        };

        configure?.Invoke(options);

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<ISqliteBackupService, SqliteBackupService>();

        if (options.HealthCheck)
        {
            builder.Services
                .AddHealthChecks()
                .AddCheck<SqliteHealthCheck>($"sqlite-{connectionName}");
        }

        if (options.BackupDirectory is not null)
        {
            builder.Services.AddHostedService<SqliteBackupScheduler>();
        }

        return builder;
    }
}
