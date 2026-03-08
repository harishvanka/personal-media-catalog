# Media Catalog — Build Progress

> **Goal:** A self-hosted, API-first file inventory system for Windows 11 — a modern replacement for tools like Disk Explorer Professional.
> Built with ASP.NET Core 10, EF Core 10, SQLite.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10 (arm64) |
| API framework | ASP.NET Core 10 Web API |
| ORM | Entity Framework Core 10 |
| Database | SQLite (`mediaCatalog.db`) |
| Background processing | `BackgroundService` + `Channel<T>` queue |
| Hashing | `SHA256` streaming in 4 KB chunks |
| Package: filesystem | `System.IO.Abstractions` 19.0.1 |

---

## Modules

### Module 1 — Core Setup & Database ✅ Done

**Implemented:** 2026-03-08

#### What was built
- Solution structure: `MediaCatalog.sln` + `MediaCatalog.Api/`
- EF Core `DbContext` with SQLite, connection string in `appsettings.json`
- Two EF entities with indexes
- `GET /api/health` endpoint (DB connectivity check)
- `DesignTimeDbContextFactory` for EF migrations

#### Data model

```
Drive
  Id            int PK
  Label         string UNIQUE    e.g. "4TB_Movies"
  RootPath      string           e.g. "D:\"          ← added in Module 2
  Serial        string?
  LastScannedAt DateTime?

MediaFile
  Id            int PK
  DriveId       int FK → Drive
  RelativePath  string           INDEX
  SizeBytes     long
  Extension     string
  ContentHash   string SHA-256   INDEX
  Category      string           INDEX  (Movie / Tutorial / Document / Photo / Other)
  CreatedAtFs   DateTime?
  ModifiedAtFs  DateTime?
```

#### EF migrations

| Migration | Date | Change |
|---|---|---|
| `InitialCreate` | 2026-03-08 | Drive + MediaFile tables, all indexes |
| `AddDriveRootPath` | 2026-03-08 | Added `RootPath` column to Drive |

#### Files
```
MediaCatalog.Api/
  Program.cs
  appsettings.json
  MediaCatalog.Api.csproj
  Models/
    Drive.cs
    MediaFile.cs
  Data/
    MediaCatalogContext.cs
    DesignTimeDbContextFactory.cs
  Dtos/
    DriveDtos.cs
    MediaFileDtos.cs
  Controllers/
    HealthController.cs
  Migrations/
    20260308_InitialCreate.*
    20260308_AddDriveRootPath.*
```

---

### Module 2 — Drive Registration & Scanner ✅ Done

**Implemented:** 2026-03-09

#### What was built

A non-blocking, queue-based file scanner — the equivalent of Disk Explorer Pro's "Analysing Volume" dialog.

| Disk Explorer Pro feature | Implementation |
|---|---|
| Total folders / files / size counters | `ScanJob.TotalFolders`, `TotalFiles`, `TotalBytes` |
| Real-time progress bar | Poll `GET /api/drives/{id}/scan/{jobId}` |
| Error log panel | `ScanJob.Errors` list |
| "Unable to copy file …" entries | Caught per-file, added to `Errors` |
| Content hashing (files with included content) | `SHA256.ComputeHashAsync` with 4 KB `FileStream` |
| Skipping system folders | `$RECYCLE.BIN`, `System Volume Information`, `Recovery`, `Config.Msi` |
| Skipping junk files | `thumbs.db`, `desktop.ini`, `pagefile.sys`, `hiberfil.sys`, `swapfile.sys` |
| Re-scan (update existing entries) | Upsert by `DriveId + RelativePath` |

#### API endpoints

| Method | URL | Description |
|---|---|---|
| `POST` | `/api/drives/register` | Register a drive with label + physical path |
| `GET` | `/api/drives` | List all drives with file count + total bytes |
| `GET` | `/api/drives/{id}` | Single drive detail |
| `POST` | `/api/drives/{id}/scan` | Enqueue a background scan → returns `{ jobId, pollUrl }` |
| `GET` | `/api/drives/{id}/scan/{jobId}` | Live scan progress (poll every 1–2 s) |
| `GET` | `/api/drives/{id}/scan` | All scan jobs for a drive |

#### Category detection rules

| Category | Triggers |
|---|---|
| `Movie` | `.mkv .mp4 .avi .mov .wmv .m4v .ts .iso` |
| `Document` | `.pdf .docx .doc .pptx .xlsx .txt .md` |
| `Photo` | `.jpg .jpeg .png .heic .gif .bmp .tiff .raw .cr2 .nef` |
| `Tutorial` | Path contains: `Udemy`, `Coursera`, `PluralSight`, `LinkedIn`, `Tutorial`, `Course` |
| `Other` | Everything else |

#### DI registration pattern

```csharp
// Singleton registered once, exposed via two abstractions:
builder.Services.AddSingleton<DriveScannerService>();
builder.Services.AddSingleton<IDriveScanner>(sp => sp.GetRequiredService<DriveScannerService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<DriveScannerService>());
```

#### Files added
```
MediaCatalog.Api/
  Models/
    ScanJob.cs            in-memory scan progress model
  Services/
    IDriveScanner.cs      interface: EnqueueAsync()
    ScanJobTracker.cs     singleton ConcurrentDictionary of jobs
    DriveScannerService.cs BackgroundService + IDriveScanner impl
  Controllers/
    DrivesController.cs   5 endpoints
```

#### Files modified
```
Models/Drive.cs           + RootPath property
Dtos/DriveDtos.cs         + DriveStatsDto, updated DriveDto
Program.cs                + service registrations, await app.RunAsync()
MediaCatalog.Api.csproj   net8.0 → net10.0, EF packages → 10.0.3
```

#### Sample workflow

```bash
# 1. Register a drive
curl -X POST http://localhost:5000/api/drives/register \
  -H "Content-Type: application/json" \
  -d '{ "label": "4TB_Movies", "path": "D:\\", "serial": "XYZ123" }'

# 2. Start a scan
curl -X POST http://localhost:5000/api/drives/1/scan
# → { "jobId": "abc-123", "pollUrl": "/api/drives/1/scan/abc-123" }

# 3. Poll progress
curl http://localhost:5000/api/drives/1/scan/abc-123
# → { "status": "Running", "totalFiles": 842, "totalBytes": 73234567, "errors": [] }
```

---

### Module 3 — Search & Duplicate Detection ⬜ Not started

**Planned endpoints**

| Method | URL | Description |
|---|---|---|
| `GET` | `/api/search/files` | Full-text path search with category + drive filters |
| `GET` | `/api/duplicates/summary` | Count of duplicate groups, wasted space |
| `GET` | `/api/duplicates/by-hash/{hash}` | All locations for a given SHA-256 hash |

**Files to create**
```
Controllers/
  SearchController.cs
  DuplicatesController.cs
```

---

### Module 4 — File Organization Engine ⬜ Not started

**Planned endpoints**

| Method | URL | Description |
|---|---|---|
| `GET` | `/api/organize/{fileId}/suggest` | Suggest a clean target path based on filename rules |
| `POST` | `/api/organize/{fileId}/move` | Move file on disk + update DB (supports `dryRun`) |

**Naming rules planned**
- Movies → `Movies/{Year}/{CleanTitle}.mkv`
- Documents → `Docs/{ext}/{filename}`
- Photos → `Photos/{Year}/{filename}`
- Tutorials → `Tutorials/{Platform}/{CourseName}/…`

**Files to create**
```
Services/
  IFileOrganizer.cs
  FileOrganizer.cs
Controllers/
  OrganizeController.cs
```

---

### Module 5 — Web UI ⬜ Not started

Razor Pages served from the same ASP.NET Core app. No SPA framework — plain HTML + `fetch`.

**Planned pages**

| Page | Features |
|---|---|
| `Drives` | List drives, last scan date, trigger scan button, live progress dialog |
| `Search` | Text box + category/drive filters, results table |
| `Duplicates` | Summary card + expandable per-hash location list |

---

### Module 6 — Mobile-Ready API ⬜ Not started

- JWT authentication (`POST /api/auth/login` → Bearer token)
- CORS policy for LAN mobile devices (React Native / Expo)
- `[Authorize]` on scan, organize, and duplicate endpoints

---

## Running the Project

```bash
cd MediaCatalog.Api

# Apply migrations (first time or after model changes)
dotnet ef database update

# Run the API
dotnet run

# API base URL
http://localhost:5000
http://localhost:5000/api/health
```

## Notes

- SQLite database file: `MediaCatalog.Api/mediaCatalog.db`
- Scan jobs are **in-memory only** — lost on app restart (by design; no need to persist job history)
- Multiple drives can be scanned concurrently; the `Channel` queue serialises them one at a time (safe for SQLite)
- SHA-256 hash uses `FileShare.Read` so files open in media players can still be hashed
