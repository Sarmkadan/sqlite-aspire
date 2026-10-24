using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace SqliteAspire;

/// <summary>
/// Registers SQLite services in an application that receives its connection string from configuration
/// (the runtime side of a <see cref="SqliteResource"/>).
/// </summary>
public static class SqliteClientExtensions
{
    /// <summary>
    /// Reads <c>ConnectionStrings:{connectionName}</c>, then <c>Sqlite:{connectionName}</c>, then applies <paramref name="configure"/>,
    /// and registers <see cref="SqliteOptions"/>, <see cref="ISqliteBackupService"/>, the health check and the backup scheduler
    /// according to the result. The same options are also registered as named <see cref="IOptions{TOptions}"/> under <paramref name="connectionName"/>.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="connectionName">Name of the connection string, as used by the app host resource.</param>
    /// <param name="configure">Optional callback to adjust the options after configuration has been bound.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <remarks>
    /// <paramref name="configure"/> runs once for the eagerly built <see cref="SqliteOptions"/> and again whenever the named
    /// <see cref="IOptions{TOptions}"/> is resolved, so it should be free of side effects.
    /// The appsettings shape for <c>Sqlite:{connectionName}</c> is documented on <see cref="SqliteOptions"/>.
    /// </remarks>
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
        var section = builder.Configuration.GetSection($"{SqliteOptions.SectionName}:{connectionName}");

        var options = new SqliteOptions();
        Populate(options, connectionStringBuilder, section, configure);

        builder.Services.AddOptions<SqliteOptions>(connectionName)
            .Configure(target => Populate(target, connectionStringBuilder, section, configure));

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

    /// <summary>
    /// Fills <paramref name="target"/> from the connection string, binds the configuration section over it,
    /// then applies the optional <paramref name="configure"/> callback.
    /// </summary>
    private static void Populate(
        SqliteOptions target,
        SqliteConnectionStringBuilder connectionStringBuilder,
        IConfigurationSection section,
        Action<SqliteOptions>? configure)
    {
        target.DatabasePath = connectionStringBuilder.DataSource;
        target.Pooling = connectionStringBuilder.Pooling;
        target.BusyTimeout = TimeSpan.FromSeconds(connectionStringBuilder.DefaultTimeout);

        section.Bind(target);
        configure?.Invoke(target);
    }
}
