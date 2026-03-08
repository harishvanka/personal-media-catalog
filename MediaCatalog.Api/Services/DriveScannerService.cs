using System.Security.Cryptography;
using System.Threading.Channels;
using MediaCatalog.Api.Data;
using MediaCatalog.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MediaCatalog.Api.Services
{
    /// <summary>
    /// Long-running background service that processes scan requests from a bounded channel.
    /// Each scan walks the drive tree, hashes files in 4 KB chunks, and upserts MediaFile rows.
    /// Mirrors the "Analysing Volume" dialog from Disk Explorer Pro.
    /// </summary>
    public sealed class DriveScannerService : BackgroundService, IDriveScanner
    {
        // Bounded to 10 so callers get natural back-pressure if the queue is full.
        private readonly Channel<(int DriveId, ScanJob Job)> _queue =
            Channel.CreateBounded<(int, ScanJob)>(new BoundedChannelOptions(10)
            {
                FullMode = BoundedChannelFullMode.Wait
            });

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DriveScannerService> _logger;

        // Folder/file names to skip (system artifacts)
        private static readonly HashSet<string> SkippedFolders = new(StringComparer.OrdinalIgnoreCase)
        {
            "$RECYCLE.BIN", "System Volume Information", "Recovery", "Config.Msi"
        };

        private static readonly HashSet<string> SkippedFiles = new(StringComparer.OrdinalIgnoreCase)
        {
            "thumbs.db", "desktop.ini", "pagefile.sys", "hiberfil.sys", "swapfile.sys"
        };

        public DriveScannerService(IServiceScopeFactory scopeFactory, ILogger<DriveScannerService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        // IDriveScanner: enqueue and return immediately
        public async Task EnqueueAsync(int driveId, ScanJob job, CancellationToken cancellationToken = default) =>
            await _queue.Writer.WriteAsync((driveId, job), cancellationToken);

        // BackgroundService: process queue items one at a time
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var (driveId, job) in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                await ProcessScanAsync(driveId, job, stoppingToken);
            }
        }

        private async Task ProcessScanAsync(int driveId, ScanJob job, CancellationToken ct)
        {
            job.Status = ScanStatus.Running;
            job.StartedAt = DateTime.UtcNow;

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaCatalogContext>();

            var drive = await db.Drives.FindAsync(new object[] { driveId }, ct);
            if (drive is null)
            {
                Fail(job, $"Drive ID {driveId} not found.");
                return;
            }

            if (!Directory.Exists(drive.RootPath))
            {
                Fail(job, $"Root path '{drive.RootPath}' does not exist or is inaccessible.");
                return;
            }

            _logger.LogInformation("Scan started: {Label} → {Path}", drive.Label, drive.RootPath);

            try
            {
                var pending = new Stack<string>();
                pending.Push(drive.RootPath);

                while (pending.Count > 0)
                {
                    if (ct.IsCancellationRequested) break;

                    var currentDir = pending.Pop();
                    job.TotalFolders++;

                    // Enumerate sub-directories safely
                    try
                    {
                        foreach (var sub in Directory.EnumerateDirectories(currentDir))
                        {
                            if (!SkippedFolders.Contains(Path.GetFileName(sub)))
                                pending.Push(sub);
                        }
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        job.Errors.Add($"Access denied (folder): {currentDir} — {ex.Message}");
                        continue;
                    }

                    // Enumerate files safely
                    IEnumerable<string> files;
                    try
                    {
                        files = Directory.EnumerateFiles(currentDir);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        job.Errors.Add($"Access denied (files): {currentDir} — {ex.Message}");
                        continue;
                    }

                    foreach (var filePath in files)
                    {
                        if (ct.IsCancellationRequested) break;

                        if (SkippedFiles.Contains(Path.GetFileName(filePath)))
                            continue;

                        await ProcessFileAsync(db, drive, job, filePath, ct);

                        // Save in batches of 100 to avoid holding a large transaction
                        if (job.ProcessedFiles % 100 == 0)
                        {
                            await db.SaveChangesAsync(ct);
                            _logger.LogInformation("{Label}: {N} files processed", drive.Label, job.ProcessedFiles);
                        }
                    }
                }

                await db.SaveChangesAsync(ct);
                drive.LastScannedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);

                job.Status = ScanStatus.Completed;
                job.CompletedAt = DateTime.UtcNow;
                _logger.LogInformation(
                    "Scan complete: {Label} — {Files} files, {Folders} folders, {Bytes:N0} bytes, {Errors} errors",
                    drive.Label, job.TotalFiles, job.TotalFolders, job.TotalBytes, job.Errors.Count);
            }
            catch (OperationCanceledException)
            {
                Fail(job, "Scan was cancelled.");
            }
            catch (Exception ex)
            {
                Fail(job, $"Unexpected error: {ex.Message}");
                _logger.LogError(ex, "Scan failed for drive {DriveId}", driveId);
            }
        }

        private async Task ProcessFileAsync(
            MediaCatalogContext db, Drive drive, ScanJob job, string filePath, CancellationToken ct)
        {
            try
            {
                var info = new FileInfo(filePath);
                var relativePath = Path.GetRelativePath(drive.RootPath, filePath);
                var extension = info.Extension.ToLowerInvariant();
                var category = DetectCategory(relativePath, extension);

                var hash = await ComputeHashAsync(filePath, job, ct);

                // Upsert: update if the file was already catalogued on a previous scan
                var existing = await db.MediaFiles
                    .FirstOrDefaultAsync(f => f.DriveId == drive.Id && f.RelativePath == relativePath, ct);

                if (existing is null)
                {
                    db.MediaFiles.Add(new MediaFile
                    {
                        DriveId = drive.Id,
                        RelativePath = relativePath,
                        SizeBytes = info.Length,
                        Extension = extension,
                        ContentHash = hash,
                        Category = category,
                        CreatedAtFs = info.CreationTime,
                        ModifiedAtFs = info.LastWriteTime
                    });
                }
                else
                {
                    existing.SizeBytes = info.Length;
                    existing.ContentHash = hash;
                    existing.Category = category;
                    existing.ModifiedAtFs = info.LastWriteTime;
                }

                job.TotalFiles++;
                job.TotalBytes += info.Length;
                job.ProcessedFiles++;
            }
            catch (Exception ex)
            {
                // Mirrors Disk Explorer Pro's "Unable to copy file" error log entry
                job.Errors.Add($"Unable to process file {filePath} — {ex.Message}");
            }
        }

        private async Task<string> ComputeHashAsync(string filePath, ScanJob job, CancellationToken ct)
        {
            try
            {
                using var sha256 = SHA256.Create();
                // FileShare.Read allows hashing files that are already open (e.g. by media players)
                await using var stream = new FileStream(
                    filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
                var bytes = await sha256.ComputeHashAsync(stream, ct);
                return Convert.ToHexString(bytes).ToLowerInvariant();
            }
            catch (Exception ex)
            {
                job.Errors.Add($"Unable to hash file {filePath} — {ex.Message}");
                return string.Empty;
            }
        }

        private static string DetectCategory(string relativePath, string extension) => extension switch
        {
            ".mkv" or ".mp4" or ".avi" or ".mov" or ".wmv" or ".m4v" or ".ts" or ".iso" => "Movie",
            ".pdf" or ".docx" or ".doc" or ".pptx" or ".xlsx" or ".txt" or ".md" => "Document",
            ".jpg" or ".jpeg" or ".png" or ".heic" or ".gif" or ".bmp" or ".tiff"
                or ".raw" or ".cr2" or ".nef" => "Photo",
            _ when ContainsTutorialKeyword(relativePath) => "Tutorial",
            _ => "Other"
        };

        private static bool ContainsTutorialKeyword(string path) =>
            path.Contains("Udemy", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("Coursera", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("PluralSight", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("LinkedIn", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("Tutorial", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("Course", StringComparison.OrdinalIgnoreCase);

        private static void Fail(ScanJob job, string message)
        {
            job.Status = ScanStatus.Failed;
            job.Errors.Add(message);
        }
    }
}
