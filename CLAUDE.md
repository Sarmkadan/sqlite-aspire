# sqlite-aspire

SQLite as a first-class .NET Aspire resource: WAL mode, connection pooling, scheduled backups, health checks.

## Commands

```bash
dotnet build                                  # full solution, warnings are errors
dotnet test                                   # xunit, tests/sqlite-aspire.Tests
dotnet test --filter "FullyQualifiedName~Backup"
dotnet pack src/sqlite-aspire -c Release -o artifacts
dotnet format                                 # apply .editorconfig
```

Target: net9.0. If the local SDK is .NET 10 only, `dotnet build` still works (targeting pack from NuGet); `dotnet test` needs `DOTNET_ROLL_FORWARD=Major` or a 9.0 runtime.

## Layout

```
src/sqlite-aspire/            library, PackageId SqliteAspire, namespace SqliteAspire
  SqliteResource.cs           Aspire resource model (Resource + IResourceWithConnectionString)
  SqliteResourceBuilderExtensions.cs  AddSqlite / WithWalMode / WithConnectionPooling / WithBackup / WithHealthCheck
  SqliteOptions.cs            all tunables + ToConnectionString()
  ISqliteBackupService.cs     backup/restore/list/prune contract + default impl
  SqliteHealthCheck.cs        IHealthCheck (open + journal_mode check + PRAGMA quick_check)
tests/sqlite-aspire.Tests/    xunit
```

## Architecture

- App host side: `AddSqlite` registers `SqliteResource`; `With*` extensions mutate `SqliteResource.Options` before the model is frozen. `ConnectionStringExpression` is built from `Options.ToConnectionString()`.
- Runtime side (dependent project): connection string arrives as `ConnectionStrings:{name}`. `SqliteHealthCheck` and `SqliteBackupService` are constructed from `SqliteOptions` and registered in DI by an `AddSqliteClient`-style extension (to be added).
- WAL and pragmas are applied once per file on first open, not per connection. Pooling is delegated to Microsoft.Data.Sqlite (`Pooling=True`).
- Backups use the online backup API (`SqliteConnection.BackupDatabase` or `VACUUM INTO`), never file copy of a live WAL database.

## Conventions

- File-scoped namespaces, primary constructors, `sealed` by default.
- Public API fully XML-documented; `GenerateDocumentationFile` is on and CS1591 is an error.
- No `NotImplementedException` left in `main` after a feature is claimed done; stubs exist only in the scaffold.
- Tests create a fresh database file under `Path.GetTempPath()` per test and delete it in `Dispose`.
- Dependencies: Aspire.Hosting, Microsoft.Data.Sqlite, Microsoft.Extensions.Diagnostics.HealthChecks. Do not add more without a reason in the PR.
