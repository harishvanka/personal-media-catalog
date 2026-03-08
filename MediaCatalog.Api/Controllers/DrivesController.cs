using MediaCatalog.Api.Data;
using MediaCatalog.Api.Dtos;
using MediaCatalog.Api.Models;
using MediaCatalog.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediaCatalog.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DrivesController : ControllerBase
    {
        private readonly MediaCatalogContext _context;
        private readonly IDriveScanner _scanner;
        private readonly ScanJobTracker _tracker;

        public DrivesController(MediaCatalogContext context, IDriveScanner scanner, ScanJobTracker tracker)
        {
            _context = context;
            _scanner = scanner;
            _tracker = tracker;
        }

        // GET /api/drives
        // Returns all registered drives with live file count and total size (like the Disk Explorer volume list)
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var drives = await _context.Drives
                .Select(d => new DriveStatsDto(
                    d.Id,
                    d.Label,
                    d.RootPath,
                    d.Serial,
                    d.LastScannedAt,
                    d.MediaFiles!.Count(),
                    d.MediaFiles!.Sum(f => f.SizeBytes)))
                .ToListAsync();

            return Ok(drives);
        }

        // GET /api/drives/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var drive = await _context.Drives.FindAsync(id);
            return drive is null ? NotFound() : Ok(drive);
        }

        // POST /api/drives/register
        // Body: { "label": "4TB_Movies", "path": "D:\\", "serial": "XYZ" }
        [HttpPost("register")]
        public async Task<IActionResult> Register(CreateDriveDto dto)
        {
            if (await _context.Drives.AnyAsync(d => d.Label == dto.Label))
                return Conflict(new { error = $"A drive with label '{dto.Label}' already exists." });

            if (!Directory.Exists(dto.Path))
                return BadRequest(new { error = $"Path '{dto.Path}' does not exist on this machine." });

            var drive = new Drive { Label = dto.Label, RootPath = dto.Path, Serial = dto.Serial };
            _context.Drives.Add(drive);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = drive.Id },
                new DriveDto(drive.Id, drive.Label, drive.RootPath, drive.Serial, drive.LastScannedAt));
        }

        // POST /api/drives/{id}/scan
        // Enqueues a background scan; returns a jobId to poll for progress
        [HttpPost("{id}/scan")]
        public async Task<IActionResult> StartScan(int id)
        {
            var drive = await _context.Drives.FindAsync(id);
            if (drive is null) return NotFound();

            var job = _tracker.Create(id);
            await _scanner.EnqueueAsync(id, job, HttpContext.RequestAborted);

            return Accepted(new
            {
                jobId = job.Id,
                status = job.Status.ToString(),
                pollUrl = Url.Action(nameof(GetScanStatus), new { id, jobId = job.Id })
            });
        }

        // GET /api/drives/{id}/scan/{jobId}
        // Returns real-time progress — poll this every 1-2 s to drive a progress UI
        [HttpGet("{id}/scan/{jobId}")]
        public IActionResult GetScanStatus(int id, Guid jobId)
        {
            var job = _tracker.Get(jobId);
            if (job is null || job.DriveId != id) return NotFound();

            return Ok(new
            {
                job.Id,
                job.DriveId,
                Status = job.Status.ToString(),
                job.TotalFolders,
                job.TotalFiles,
                job.TotalBytes,
                job.ProcessedFiles,
                job.Errors,
                job.StartedAt,
                job.CompletedAt
            });
        }

        // GET /api/drives/{id}/scan   (list all jobs for a drive)
        [HttpGet("{id}/scan")]
        public IActionResult GetAllScanJobs(int id)
        {
            var jobs = _tracker.GetForDrive(id)
                .OrderByDescending(j => j.StartedAt)
                .Select(j => new
                {
                    j.Id,
                    Status = j.Status.ToString(),
                    j.TotalFiles,
                    j.TotalBytes,
                    j.Errors.Count,
                    j.StartedAt,
                    j.CompletedAt
                });

            return Ok(jobs);
        }
    }
}
