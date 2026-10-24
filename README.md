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
