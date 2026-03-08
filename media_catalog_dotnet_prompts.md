# Media Catalog App – ASP.NET Core + SQLite
## Modular Prompt Set for Code Generation

You are an expert C# and .NET developer.  
Build a production‑ready **media catalog application** for personal file inventory on Windows 11.

The user has multiple internal/external hard disks with:
- Technical video tutorials
- Documents
- Movies
- Photos

They want to:
- Scan each drive, label it, and store inventory in a database
- See where any file is located
- Avoid duplicates using file hashes
- Organize files into better folder structures
- Later access the catalog from mobile apps (API‑first design)

The target environment is:
- Windows 11 desktop
- Ryzen CPU
- Self‑hosted only (no cloud dependency)

---

## GLOBAL REQUIREMENTS (APPLY TO ALL MODULES)

- **Tech stack**
  - ASP.NET Core 8 (or latest LTS) Web API
  - C# 12
  - Entity Framework Core (with migrations)
  - SQLite as the database
  - Background services / hosted services for scanning
  - Minimal APIs or traditional Controllers (your choice, but be consistent)
  - Strong typing, async/await everywhere

- **Data model (MANDATORY – use EXACT semantics)**
  - `Drive` entity:
    - `Id` (int, PK)
    - `Label` (string, unique) – e.g. `"4TB_Movies"`
    - `Serial` (string, optional) – disk serial or signature
    - `LastScannedAt` (DateTime? nullable)
  - `MediaFile` entity:
    - `Id` (int, PK)
    - `DriveId` (FK → Drive)
    - `RelativePath` (string, path relative to drive root)
    - `SizeBytes` (long)
    - `Extension` (string)
    - `ContentHash` (string, SHA‑256, unique when possible)
    - `Category` (string; e.g. `"Movie"`, `"Tutorial"`, `"Document"`, `"Photo"`)
    - `CreatedAtFs` (DateTime?)
    - `ModifiedAtFs` (DateTime?)
  - Indexes:
    - On `ContentHash`
    - On `DriveId`
    - On `RelativePath`
    - On `Category`

- **Hashing**
  - Use `SHA256` from `System.Security.Cryptography`
  - Stream files in chunks (e.g. 4 KB) to handle large video files
  - Never load whole files into memory

- **Filesystem**
  - Use `System.IO` and `System.IO.Abstractions` (if needed) or at least structure code so it’s testable
  - Use `Path` and `Directory` APIs; handle Windows paths safely

- **General quality**
  - Use dependency injection
  - Add logging (Microsoft.Extensions.Logging)
  - Use DTOs for requests/responses (no direct EF entities on wire)
  - Proper error handling and HTTP status codes
  - Comment where design decisions are non‑obvious

---

## MODULE 1 – Core Solution & Database Setup

**Prompt to send:**

You are now implementing **Module 1: Core Setup & Data Access**.

Generate a .NET solution with this structure:

```text
MediaCatalog/
├── MediaCatalog.Api/          # ASP.NET Core Web API
│   ├── Program.cs
│   ├── appsettings.json
│   ├── Models/
│   │   ├── Drive.cs
│   │   └── MediaFile.cs
│   ├── Data/
│   │   ├── MediaCatalogContext.cs
│   │   └── DesignTimeDbContextFactory.cs
│   ├── Dtos/
│   │   ├── DriveDtos.cs
│   │   └── MediaFileDtos.cs
│   ├── Controllers/
│   │   └── HealthController.cs
│   └── Migrations/            # EF Core migrations
└── MediaCatalog.sln
```

Requirements:

1. Configure **SQLite** via EF Core:
    - Connection string in `appsettings.json`
    - `MediaCatalogContext` with `DbSet<Drive>` and `DbSet<MediaFile>`
    - Fluent configuration for indexes (ContentHash, DriveId, RelativePath, Category)
2. Implement basic **HealthController**:
    - `GET /api/health` → returns status `"OK"` and DB connectivity check
3. Provide commands (as comments) for:
    - `dotnet ef migrations add InitialCreate`
    - `dotnet ef database update`
4. Use nullable reference types and `async` (`Task<IActionResult>` where applicable).

Output:

- All source code files for this structure
- No scanning logic yet; this is just core setup and model definitions.

---

## MODULE 2 – Drive Registration \& Scanning Background Service

**Prompt to send after Module 1 is done:**

You are now implementing **Module 2: Drive Management \& Scanner** on top of the previous module.

Extend the existing solution with:

```text
MediaCatalog.Api/
├── Controllers/
│   ├── DrivesController.cs
├── Services/
│   ├── IDriveScanner.cs
│   └── DriveScannerService.cs
├── Models/          # (already exists)
├── Data/            # (already exists)
└── ...
```

New features:

1. **Drive registration \& listing**
    - `POST /api/drives/register`
        - Request: `{ "label": "4TB_Movies", "path": "E:\\" , "serial": "XYZ123" }`
        - Stores drive in DB; label must be unique
    - `GET /api/drives`
        - Returns list of drives with:
            - Id, Label, Serial, LastScannedAt
            - File count
            - Total size in bytes
2. **Scanning API**
    - `POST /api/drives/{id}/scan`
        - Triggers scan as a **background operation** (no blocking)
        - Returns a scan job id or simple `"started": true`
    - Implement `DriveScannerService` as a `BackgroundService` or hosted service:
        - Uses a queue (e.g. `Channel<DriveScanRequest>`) to process scan requests
        - Walks all files under the given physical drive path
        - For each file:
            - Collect metadata
            - Compute SHA‑256 hash via streaming
            - Insert or update `MediaFile` row
        - Skip known system folders: `"$RECYCLE.BIN"`, `"System Volume Information"`, `"thumbs.db"` etc.
3. Design:
    - `IDriveScanner` interface
    - Use dependency injection for scanner service
    - Log progress (e.g. every N files) via `ILogger`
4. For now, scan status can be simple:
    - Just update `Drive.LastScannedAt` when done
    - (Detailed job tracking can come later modules)

Output:

- Controllers, service interfaces and implementations, and necessary updates in `Program.cs` (DI registration).

---

## MODULE 3 – Search \& Duplicate Detection APIs

**Prompt to send:**

You are now implementing **Module 3: Search and Duplicate APIs** on top of previous modules.

Add:

```text
MediaCatalog.Api/
├── Controllers/
│   ├── SearchController.cs
│   └── DuplicatesController.cs
```

Features:

1. **Search**
    - Endpoint: `GET /api/search/files`
    - Query parameters:
        - `query` (string, required)
        - `category` (optional: Movie, Tutorial, Document, Photo)
        - `driveLabel` (optional)
    - Behavior:
        - Search `RelativePath` via `LIKE` (`Contains` in LINQ)
        - Apply filters by category and drive label
        - Order by match quality and then by path
    - Response:
        - A list of DTOs with drive label, relative path, size, category, modified date
2. **Duplicate summary**
    - Endpoint: `GET /api/duplicates/summary`
    - Behavior:
        - Group by `ContentHash`
        - Keep groups where count > 1
        - Return:
            - total duplicate groups
            - total duplicate files
            - approximate wasted space (bytes)
3. **Duplicate locations**
    - Endpoint: `GET /api/duplicates/by-hash/{hash}`
    - Return all file records with that hash, including drive label and path.

Implementation details:

- Use EF Core LINQ, no raw SQL.
- Map entities → DTOs.
- Properly handle empty results (return 200 with empty list, not 404).

---

## MODULE 4 – Organization Suggestions \& Move Operations

**Prompt to send:**

You are now implementing **Module 4: File Organization Engine**.

Add:

```text
MediaCatalog.Api/
├── Services/
│   ├── IFileOrganizer.cs
│   └── FileOrganizer.cs
├── Controllers/
│   └── OrganizeController.cs
```

Features:

1. **Suggest organization path**
    - Endpoint: `GET /api/organize/{fileId}/suggest`
    - Look up file by Id, inspect extension and filename.
    - Rules:
        - Movies (`.mkv`, `.mp4`, `.avi`):
            - Pattern: `Movies/{Year}/{CleanTitle}{Extension}`
            - Try to extract year from file name (like `Some.Movie.2024.1080p.mkv`)
            - `CleanTitle` should remove resolution tags, dots, and extra tokens
        - Tutorials (folders containing `Udemy`, `PluralSight`, `Coursera`):
            - `Tutorials/{Platform}/{CourseName}/...`
        - Documents (`.pdf`, `.docx`, `.pptx`):
            - `Docs/{Extension}/{FileName}{Extension}`
        - Photos (`.jpg`, `.jpeg`, `.png`, `.heic`):
            - `Photos/{Year}/{FileName}{Extension}` (year from file date)
    - Return DTO: `{ "suggestedRelativePath": "Movies/2024/Inception.mkv" }`
2. **Move file**
    - Endpoint: `POST /api/organize/{fileId}/move`
    - Request: `{ "targetRelativePath": "Movies/2024/Inception.mkv", "dryRun": true/false }`
    - Behavior:
        - Resolve physical source and target path using the drive’s root path
        - If `dryRun = true`: do NOT move, just return what *would* happen
        - If `dryRun = false`: perform `File.Move` (create directories as needed)
        - Update `MediaFile.RelativePath` in DB when successful
3. Safety considerations:
    - Validate that the target path is on the same drive
    - Protect against path traversal (`..`)
    - Handle existing file at target (option: fail or add suffix)

---

## MODULE 5 – Minimal Web UI (Optional)

**Prompt to send (optional if API only is enough):**

You are now implementing **Module 5: Minimal Web UI**.

Goal: simple HTML UI for desktop browser, served from the same ASP.NET Core app.

Add:

```text
MediaCatalog.Api/
├── Pages/
│   ├── Index.cshtml
│   ├── Drives.cshtml
│   ├── Search.cshtml
│   └── Duplicates.cshtml
├── wwwroot/
│   └── css/site.css
```

Requirements:

- Use Razor Pages or MVC Views (your choice).
- Features:
    - Drives page: list drives, show last scan date, button to trigger scan
    - Search page: text box + filters; shows results from `/api/search/files`
    - Duplicates page: summary and per‑hash listing
- Use lightweight JS (fetch API) to call the Web API endpoints.
- Make it work with zero SPA frameworks, just plain views + small JS.

---

## MODULE 6 – Mobile‑Ready API Considerations

**Prompt to send:**

You are now implementing **Module 6: Mobile Integration Readiness**.

Extend the existing API with:

- CORS policy that can allow local network mobile devices
- JWT‑based auth (simple username/password, issuing JWT)
- Attribute‑based authorization on key endpoints

Deliver:

- AuthController:
    - `POST /api/auth/login` → returns JWT on valid credentials
- Configuration:
    - Add JWT auth to Program.cs
    - Add `[Authorize]` to scan, organize, and duplicate endpoints
- CORS:
    - Named policy, e.g. `"MobileApp"` for `http://localhost:19006`, etc. (React Native / Expo), plus LAN origins

Explain in comments how a mobile app would:

- Call `/api/auth/login`
- Store JWT
- Call search endpoints with Bearer token

---

## HOW TO USE THESE PROMPTS

1. Start with **Module 1**, paste that section into the model.
2. Generate code, create a solution locally, run migrations, test `/api/health`.
3. Then proceed module‑by‑module (2 → 3 → 4 → 5 → 6).
4. At each step, you can ask for refinements, tests, or bug fixes.

Make sure you ALWAYS build on the previously defined structure and do NOT re‑invent the project layout unless explicitly asked.