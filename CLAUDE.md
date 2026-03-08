# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Personal Media Catalog — a self-hosted file inventory system for Windows 11 (ASP.NET Core 10 + SQLite). Indexes drives, computes SHA-256 hashes, tracks scan jobs, and provides a REST API. Planned 6 modules; Modules 1 & 2 are complete.

## Commands

All commands run from the solution root or `MediaCatalog.Api/`:

```bash
# Build
dotnet build

# Run API (listens on http://localhost:5000)
cd MediaCatalog.Api && dotnet run

# Apply EF Core migrations
cd MediaCatalog.Api && dotnet ef database update

# Add a new migration
cd MediaCatalog.Api && dotnet ef migrations add <MigrationName>
```

No test project exists yet.

## Architecture

**Entry point:** [MediaCatalog.Api/Program.cs](MediaCatalog.Api/Program.cs) — registers DI, configures SQLite via EF Core, maps controllers.

**Key layers:**

- **Controllers/** — thin HTTP layer; delegates to services
  - `DrivesController` — drive registration, listing, scan enqueueing, job polling
  - `HealthController` — DB connectivity check
- **Services/** — business logic
  - `DriveScannerService` — implements both `BackgroundService` and `IDriveScanner`; uses a bounded `Channel<T>` (capacity 10) to queue scan requests; walks the directory tree depth-first, computes SHA-256 per file (4 KB streaming chunks), upserts `MediaFile` rows in batches of 100
  - `ScanJobTracker` — singleton `ConcurrentDictionary<Guid, ScanJob>` for in-memory job state (not persisted)
- **Models/** — EF Core entities: `Drive`, `MediaFile`, `ScanJob`
- **Dtos/** — API request/response contracts (separate from entities)
- **Data/** — `MediaCatalogContext` (SQLite); indexes on Label (unique), ContentHash, DriveId, RelativePath, Category
- **Migrations/** — EF Core migration history

**Database:** SQLite file at `MediaCatalog.Api/mediaCatalog.db` (created on first run/migration).

**Scan behavior:** Skips system folders (`$RECYCLE.BIN`, `System Volume Information`, `Recovery`, `Config.Msi`) and junk files (`thumbs.db`, `desktop.ini`, `pagefile.sys`, etc.). Categories derived from file extension and path keywords.

## Planned Modules (not yet implemented)

- Module 3: Search & duplicate detection APIs
- Module 4: File organization engine
- Module 5: Web UI (Razor Pages)
- Module 6: JWT auth + CORS for mobile

See [PROGRESS.md](PROGRESS.md) for detailed module status and implementation notes.
