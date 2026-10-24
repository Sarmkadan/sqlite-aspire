using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SqliteAspire;

/// <summary>
/// Health check that opens a connection, verifies the journal mode matches <see cref="SqliteOptions.WalMode"/>
/// and runs <c>PRAGMA quick_check</c>. Reports <see cref="HealthStatus.Degraded"/> when the file is readable
/// but the integrity check reports problems.
/// </summary>
/// <param name="options">Options of the database to check.</param>
public sealed class SqliteHealthCheck(SqliteOptions options) : IHealthCheck
{
    private readonly SqliteOptions _options = options;

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object> { ["path"] = _options.DatabasePath };
        var issues = new List<string>();

        try
        {
            await using var connection = new SqliteConnection(_options.ToConnectionString());
            await connection.OpenAsync(cancellationToken);

            var journalMode = (await ExecuteScalarAsync(connection, "PRAGMA journal_mode", cancellationToken))?.ToString() ?? string.Empty;
            data["journal_mode"] = journalMode;

            data["page_count"] = Convert.ToInt64(
                await ExecuteScalarAsync(connection, "PRAGMA page_count", cancellationToken) ?? 0L);

            if (_options.DatabasePath != ":memory:")
            {
                var expectedMode = _options.WalMode ? "wal" : "delete";
                if (!string.Equals(journalMode, expectedMode, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add($"Journal mode is '{journalMode}', expected '{expectedMode}'");
                }
            }

            var quickCheck = await ReadColumnAsync(connection, "PRAGMA quick_check", cancellationToken);
            if (quickCheck.Count != 1 || !string.Equals(quickCheck[0], "ok", StringComparison.Ordinal))
            {
                issues.Add(quickCheck.Count == 0 ? "PRAGMA quick_check returned no rows" : string.Join("; ", quickCheck));
            }
        }
        catch (Exception ex) when (ex is SqliteException or IOException)
        {
            return HealthCheckResult.Unhealthy("Cannot open database", ex, data);
        }

        return issues.Count == 0
            ? HealthCheckResult.Healthy("Database is open and passed quick_check", data)
            : HealthCheckResult.Degraded(string.Join("; ", issues), data: data);
    }

    private static async Task<object?> ExecuteScalarAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task<List<string>> ReadColumnAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var values = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }
}
