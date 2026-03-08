using MediaCatalog.Api.Data;
using MediaCatalog.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<MediaCatalogContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddLogging();

// ScanJobTracker is singleton: holds in-memory job state across requests
builder.Services.AddSingleton<ScanJobTracker>();

// Module 4: file organization engine (scoped — uses DbContext)
builder.Services.AddScoped<IFileOrganizer, FileOrganizer>();

// Register DriveScannerService once as a singleton, then expose it via two interfaces:
//   - IHostedService  → ASP.NET Core starts/stops it automatically
//   - IDriveScanner   → controllers call EnqueueAsync()
builder.Services.AddSingleton<DriveScannerService>();
builder.Services.AddSingleton<IDriveScanner>(sp => sp.GetRequiredService<DriveScannerService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<DriveScannerService>());

var app = builder.Build();

app.UseRouting();
app.MapControllers();

await app.RunAsync();

// to create the initial database structure you can run the following commands from
// the project directory (MediaCatalog.Api):
//
// dotnet ef migrations add InitialCreate
// dotnet ef database update
//
// ensure the `dotnet-ef` tool is installed (`dotnet tool install --global dotnet-ef`)
