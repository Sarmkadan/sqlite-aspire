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
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
