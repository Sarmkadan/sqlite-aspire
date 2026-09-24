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

## API overview

| Type | Purpose |
|------|---------|
| `SqliteResource` | Aspire resource; carries `SqliteOptions`, exposes `ConnectionStringExpression` |
| `SqliteResourceBuilderExtensions` | `AddSqlite`, `WithWalMode`, `WithConnectionPooling`, `WithBackup`, `WithHealthCheck` |
| `SqliteOptions` | Path, WAL, synchronous level, pooling, busy timeout, backup directory/interval/retention |
| `ISqliteBackupService` | `BackupAsync`, `RestoreAsync`, `ListBackupsAsync`, `PruneAsync` |
| `SqliteHealthCheck` | `IHealthCheck` implementation |

## Status

Scaffold. Public API shape is defined, implementations are pending. See [CLAUDE.md](CLAUDE.md) for architecture notes.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

[MIT](LICENSE) - Copyright 2026 Vladyslav Zaiets
