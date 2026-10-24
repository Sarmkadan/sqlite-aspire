# sqlite-aspire

[![CI](https://github.com/Sarmkadan/sqlite-aspire/actions/workflows/ci.yml/badge.svg)](https://github.com/Sarmkadan/sqlite-aspire/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/SqliteAspire.svg)](https://www.nuget.org/packages/SqliteAspire)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

SQLite as a first-class .NET Aspire resource with WAL mode, connection pooling, backup and health checks.

Aspire ships resources for Postgres, SQL Server, Redis and friends, but nothing for SQLite - the database most small services and dev loops actually use. This package fills the gap: one `AddSqlite("db")` in the app host gives every dependent a connection string, sane pragmas, a health check and scheduled backups.

## Features

- `AddSqlite` resource for the Aspire app host, visible in the dashboard, injected as `ConnectionStrings:{name}`
- WAL journal mode and `synchronous=NORMAL` applied once per database file
- ADO.NET connection pooling via Microsoft.Data.Sqlite with configurable busy timeout
- Online backups using the SQLite backup API (safe under concurrent writers), interval + retention
- `IHealthCheck` that opens a connection, verifies journal mode and runs `PRAGMA quick_check`
- No native dependencies beyond `Microsoft.Data.Sqlite`

## Installation

```bash
dotnet add package SqliteAspire
```

## Quick start

App host:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddSqlite("orders", "data/orders.db")
    .WithWalMode()
    .WithConnectionPooling(busyTimeout: TimeSpan.FromSeconds(10))
    .WithBackup("data/backups", interval: TimeSpan.FromMinutes(30), retentionCount: 48)
    .WithHealthCheck();

builder.AddProject<Projects.Api>("api")
    .WithReference(db);

builder.Build().Run();
```

Dependent project:

```csharp
var connectionString = builder.Configuration.GetConnectionString("orders");
using var conn = new SqliteConnection(connectionString);
```

## Dependent project setup

The dependent project receives `ConnectionStrings:orders` from the app host (via `WithReference`) and registers the runtime services with `AddSqliteClient`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddSqliteClient("orders");

var app = builder.Build();
```

`AddSqliteClient` throws `InvalidOperationException` if `ConnectionStrings:orders` is missing or empty. An optional callback adjusts the options after configuration is bound: `builder.AddSqliteClient("orders", options => options.BusyTimeout = TimeSpan.FromSeconds(10));`

Registrations:

| Registration | Lifetime | When |
|--------------|----------|------|
| `SqliteOptions` | Singleton (instance) | Always |
| `IValidateOptions<SqliteOptions>` → `SqliteOptionsValidator` | Singleton | Always; options are validated on start |
| Named options `SqliteOptions` / `"orders"` (`IOptionsMonitor` / `IOptionsSnapshot`) | Options | Always |
| `ISqliteBackupService` → `SqliteBackupService` | Singleton | Always |
| Health check `sqlite-orders` (`SqliteHealthCheck`) | Health check | `HealthCheck` is `true` (default) |
| `IHostedService` → `SqliteBackupScheduler` | Hosted service | `BackupDirectory` is set |

## Configuration

Per-connection overrides live under `Sqlite:{connectionName}`. Values in this section override the ones taken from `ConnectionStrings:{connectionName}`.

```json
{
  "Sqlite": {
    "orders": {
      "WalMode": true,
      "Synchronous": "Normal",
      "Pooling": true,
      "BusyTimeout": "00:00:05",
      "BackupDirectory": "/var/backups/orders",
      "BackupInterval": "01:00:00",
      "BackupRetentionCount": 24,
      "HealthCheck": true
    }
  }
}
```

All keys are optional. Defaults, from `SqliteOptions`:

| Key | Type | Default | Meaning |
|-----|------|---------|---------|
| `WalMode` | `bool` | `true` | Request WAL journal mode |
| `Synchronous` | `SqliteSynchronous` (`Off`, `Normal`, `Full`) | `Normal` | `PRAGMA synchronous` level |
| `Pooling` | `bool` | `true` | ADO.NET connection pooling |
| `BusyTimeout` | `TimeSpan` | `00:00:05` | Wait time on a locked database before SQLITE_BUSY |
| `BackupDirectory` | `string?` | `null` | Directory for scheduled backups; `null` disables them |
| `BackupInterval` | `TimeSpan` | `01:00:00` | Interval between scheduled backups |
| `BackupRetentionCount` | `int` | `24` | Number of most recent backups kept |
| `HealthCheck` | `bool` | `true` | Register the `sqlite-{name}` health check |

`DatabasePath` is not a configuration key here. It is read from `ConnectionStrings:{name}`.

## WAL and synchronous

`WalMode` switches the file to `PRAGMA journal_mode=WAL`. The mode is stored in the database file, so it stays set across restarts. WAL keeps recent commits in a `-wal` sidecar file and an index in `-shm` until a checkpoint merges them back. Readers do not block the writer, which is why WAL suits a service with concurrent requests. The sidecar files belong to the database: copying only `orders.db` while the database is in use can miss committed data. Use the backup API instead (see [Backups](#backups)). WAL is not available for `:memory:`; the options validator rejects that combination.

`Synchronous` sets `PRAGMA synchronous`, which controls how often SQLite calls `fsync`. It is per connection, not per file.

| Value | Behaviour in WAL mode | Trade-off | Use when |
|-------|-----------------------|-----------|----------|
| `Off` | No `fsync`. | Fastest. A power loss or OS crash can corrupt the database. | Throwaway data, tests, CI. |
| `Normal` | `fsync` only at WAL checkpoints. | Committed transactions survive an application crash. A power loss can roll back the most recent commits, but the file stays consistent. | Default. Recommended pairing with WAL. |
| `Full` | `fsync` on every commit. | Every committed transaction is durable. Slowest of the three. | Data that must not be lost, or `WalMode` set to `false`. |

`Normal` is only safe from corruption on power loss when WAL is on. With `WalMode` set to `false` (rollback journal), use `Full`.

Current limitation: `SqlitePragmaInitializer` is applied by `RestoreAsync` only (see [Status](#status)). Connections your code opens do not get the journal mode or synchronous level from this package yet. To apply them yourself, call the public `SqlitePragmaInitializer.Apply(connection, options)` after opening a connection.

## Backups

`ISqliteBackupService` is registered by `AddSqliteClient`. It has four operations:

| Method | Behaviour |
|--------|-----------|
| `Task<SqliteBackupInfo> BackupAsync(string destinationPath, CancellationToken ct = default)` | Writes a consistent snapshot to `destinationPath`. Creates missing directories. Throws `IOException` if the file already exists and `ArgumentException` if the path is the live database. |
| `Task RestoreAsync(string sourcePath, CancellationToken ct = default)` | Replaces the live database with the backup contents and re-applies the pragmas. Throws `FileNotFoundException` if the source is missing. Requires exclusive access, so stop or quiesce the writers first. |
| `Task<IReadOnlyList<SqliteBackupInfo>> ListBackupsAsync(CancellationToken ct = default)` | Lists `.db` and `.bak` files in `BackupDirectory`, newest first. Returns an empty list when the directory is not set or does not exist. |
| `Task<int> PruneAsync(CancellationToken ct = default)` | Deletes everything beyond the newest `BackupRetentionCount` files and returns how many it deleted. |

`SqliteBackupInfo` is a record with `Path` (absolute path), `CreatedAtUtc` (`DateTimeOffset`) and `SizeBytes` (`long`).

Manual backup from a minimal API endpoint:

```csharp
using SqliteAspire;

var builder = WebApplication.CreateBuilder(args);
builder.AddSqliteClient("orders");

var app = builder.Build();

app.MapPost("/admin/backups", async (ISqliteBackupService backups, SqliteOptions options, CancellationToken ct) =>
{
    // Writes files to disk: put an authorization policy on this route.
    var fileName = $"{Path.GetFileNameWithoutExtension(options.DatabasePath)}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.db";
    var destination = Path.Combine(options.BackupDirectory ?? "backups", fileName);

    // Two calls within the same second produce the same name, and the second one throws IOException.
    SqliteBackupInfo info = await backups.BackupAsync(destination, ct);

    return Results.Ok(info);
});

app.Run();
```

The response is the serialized `SqliteBackupInfo`, for example `{ "path": "/var/backups/orders/orders-20261024-093000.db", "createdAtUtc": "2026-10-24T09:30:00.1234567+00:00", "sizeBytes": 40960 }`.

### Scheduled backups

`SqliteBackupScheduler` runs as a hosted service only when `BackupDirectory` is set. Every `BackupInterval` it creates a backup, then calls `PruneAsync`. A failed backup is logged as an error and the next tick retries.

File names follow `{db}-{yyyyMMdd-HHmmss}.db`, where `{db}` is the file name of `DatabasePath` without its extension and the timestamp is UTC. For `data/orders.db` the file is `orders-20261024-093000.db`.

Retention is count-based. `BackupRetentionCount` (default `24`) keeps the newest files by creation time, which is one day at the default hourly interval. Manual backups in the same directory count toward the limit. Pruning considers every `.db` and `.bak` file in `BackupDirectory`, whatever its name, so give each database its own backup directory. Otherwise one database's pruning can delete another database's backups.

### Why the backup API and not `File.Copy`

In WAL mode a committed transaction may exist only in `orders.db-wal`. A plain copy of `orders.db` can therefore be stale. Copying `orders.db` and `orders.db-wal` one after the other is not atomic either: a checkpoint can run between the two copies, so the pair does not describe one point in time. `BackupDatabase` (`sqlite3_backup`) reads the source inside one consistent snapshot and writes a single complete database file, without a sidecar. Writers can keep running during the backup.

## Health checks

When `HealthCheck` is `true` (the default), `AddSqliteClient` registers `SqliteHealthCheck` as `sqlite-{name}`. Expose it with `MapHealthChecks` as usual. Each run opens a new connection and does three things: reads `PRAGMA journal_mode`, reads `PRAGMA page_count`, and runs `PRAGMA quick_check`.

| Status | When |
|--------|------|
| `Healthy` | The database opens, the journal mode matches `WalMode` (`wal` when true, `delete` when false), and `quick_check` returns exactly one row, `ok`. Description: `Database is open and passed quick_check`. |
| `Degraded` | The database opens, but the journal mode differs from the expected one, or `quick_check` returns anything other than `ok` (including no rows). The description lists every issue, separated by `; `. A failed integrity check is reported as `Degraded`, not `Unhealthy`. The journal mode is not checked for `:memory:`. |
| `Unhealthy` | `SqliteException` or `IOException` is thrown while opening the file or running the checks. Description: `Cannot open database`, with the exception attached. |

Until WAL is applied to a new file (see [Status](#status)), a fresh database reports `Degraded` because its journal mode is not `wal`.

Data returned with every result:

| Key | Type | Present when |
|-----|------|--------------|
| `path` | `string` | Always. The value of `DatabasePath`. |
| `journal_mode` | `string` | The connection opened and `PRAGMA journal_mode` ran. Lowercase, for example `wal` or `delete`. |
| `page_count` | `long` | The connection opened and `PRAGMA page_count` ran. |

On `Unhealthy` after a failed open, only `path` is present.

## API overview

| Type | Purpose |
|------|---------|
| `SqliteResource` | Aspire resource; carries `SqliteOptions`, exposes `ConnectionStringExpression` |
| `SqliteResourceBuilderExtensions` | `AddSqlite`, `WithWalMode`, `WithConnectionPooling`, `WithBackup`, `WithHealthCheck` |
| `SqliteOptions` | Path, WAL, synchronous level, pooling, busy timeout, backup directory/interval/retention |
| `ISqliteBackupService` | `BackupAsync`, `RestoreAsync`, `ListBackupsAsync`, `PruneAsync` |
| `SqliteHealthCheck` | `IHealthCheck` implementation |

## Status

Implemented: the app host resource and `With*` extensions, `SqliteOptions` (validation and connection string building), `ISqliteBackupService` (backup, restore, list, prune), `SqliteHealthCheck`, and `AddSqliteClient` with options validation and scheduled backups. The solution builds without warnings and its 45 tests pass.

Known gap: `SqlitePragmaInitializer` is applied only during restore. Connections opened by dependent projects do not get the pragmas yet, so a new database file keeps its default journal mode and the health check reports `Degraded` until WAL is set.

See [CLAUDE.md](CLAUDE.md) for architecture notes.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

[MIT](LICENSE) - Copyright 2026 Vladyslav Zaiets
