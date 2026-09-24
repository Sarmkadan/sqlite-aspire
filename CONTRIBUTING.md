# Contributing

## Setup

```bash
git clone https://github.com/Sarmkadan/sqlite-aspire
cd sqlite-aspire
dotnet build
dotnet test
```

Requires .NET 9 SDK.

## Rules

- One feature or fix per PR. Keep the diff focused.
- `TreatWarningsAsErrors` is on. A PR that does not build clean is not reviewed.
- Every public member has XML docs. The build fails otherwise (CS1591).
- New behavior comes with tests in `tests/sqlite-aspire.Tests`. Tests use real SQLite files in a temp directory, not mocks of `SqliteConnection`.
- Follow `.editorconfig`. Run `dotnet format` before pushing.
- Commit messages: Conventional Commits (`feat:`, `fix:`, `docs:`, `test:`, `refactor:`, `chore:`).

## Reporting bugs

Open an issue with: SQLite version (`SELECT sqlite_version()`), OS, .NET SDK version, minimal repro (app host snippet + failing call), and the full exception.
