using Microsoft.Extensions.Options;

namespace SqliteAspire;

/// <summary>
/// Validates <see cref="SqliteOptions"/> when the host starts, so a misconfigured database fails fast
/// instead of on the first connection.
/// </summary>
/// <remarks>
/// Checks that <see cref="SqliteOptions.DatabasePath"/> is set, <see cref="SqliteOptions.BusyTimeout"/> is not negative,
/// backup settings are sane when <see cref="SqliteOptions.BackupDirectory"/> is set, and WAL is not requested for
/// an in-memory database. Registered by <c>AddSqliteClient</c> and enforced with <c>ValidateOnStart</c>.
/// </remarks>
public sealed class SqliteOptionsValidator : IValidateOptions<SqliteOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, SqliteOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.DatabasePath))
        {
            failures.Add($"{nameof(SqliteOptions.DatabasePath)} must not be empty.");
        }

        if (options.BusyTimeout < TimeSpan.Zero)
        {
            failures.Add($"{nameof(SqliteOptions.BusyTimeout)} must not be negative.");
        }

        if (options.BackupDirectory is not null)
        {
            if (options.BackupInterval <= TimeSpan.Zero)
            {
                failures.Add($"{nameof(SqliteOptions.BackupInterval)} must be greater than zero when {nameof(SqliteOptions.BackupDirectory)} is set.");
            }

            if (options.BackupRetentionCount < 1)
            {
                failures.Add($"{nameof(SqliteOptions.BackupRetentionCount)} must be at least 1 when {nameof(SqliteOptions.BackupDirectory)} is set.");
            }
        }

        if (options.WalMode && options.DatabasePath == ":memory:")
        {
            failures.Add($"{nameof(SqliteOptions.WalMode)} is not supported for an in-memory database; set {nameof(SqliteOptions.WalMode)} to false.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
