using Microsoft.Extensions.Options;

namespace SqliteAspire.Tests;

public class SqliteOptionsValidatorTests
{
    private readonly SqliteOptionsValidator _validator = new();

    public static TheoryData<string, SqliteOptions> InvalidOptions => new()
    {
        { nameof(SqliteOptions.DatabasePath), new SqliteOptions { DatabasePath = "" } },
        { nameof(SqliteOptions.BusyTimeout), new SqliteOptions { BusyTimeout = TimeSpan.FromSeconds(-1) } },
        {
            nameof(SqliteOptions.BackupInterval),
            new SqliteOptions { BackupDirectory = "/var/backups/orders", BackupInterval = TimeSpan.Zero }
        },
        {
            nameof(SqliteOptions.BackupRetentionCount),
            new SqliteOptions { BackupDirectory = "/var/backups/orders", BackupRetentionCount = 0 }
        },
        {
            nameof(SqliteOptions.WalMode),
            new SqliteOptions { DatabasePath = ":memory:", WalMode = true }
        },
    };

    [Fact]
    public void Validate_DefaultOptions_Succeeds()
    {
        var result = _validator.Validate(null, new SqliteOptions());

        Assert.True(result.Succeeded);
        Assert.False(result.Failed);
    }

    [Theory]
    [MemberData(nameof(InvalidOptions))]
    public void Validate_InvalidOptions_FailsNamingOffendingProperty(string propertyName, SqliteOptions options)
    {
        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains(propertyName));
    }

    [Fact]
    public void Validate_MultipleViolations_ReportsEachFailure()
    {
        var options = new SqliteOptions
        {
            DatabasePath = "",
            BusyTimeout = TimeSpan.FromSeconds(-1),
            BackupDirectory = "/var/backups/orders",
            BackupInterval = TimeSpan.Zero,
            BackupRetentionCount = 0,
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Equal(4, result.Failures.Count());
        Assert.Contains(result.Failures, failure => failure.Contains(nameof(SqliteOptions.DatabasePath)));
        Assert.Contains(result.Failures, failure => failure.Contains(nameof(SqliteOptions.BusyTimeout)));
        Assert.Contains(result.Failures, failure => failure.Contains(nameof(SqliteOptions.BackupInterval)));
        Assert.Contains(result.Failures, failure => failure.Contains(nameof(SqliteOptions.BackupRetentionCount)));
    }
}
