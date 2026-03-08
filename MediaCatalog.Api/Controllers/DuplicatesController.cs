using MediaCatalog.Api.Data;
using MediaCatalog.Api.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediaCatalog.Api.Controllers
{
    [ApiController]
    [Route("api/duplicates")]
    public class DuplicatesController : ControllerBase
    {
        private readonly MediaCatalogContext _db;

        public DuplicatesController(MediaCatalogContext db) => _db = db;

        // GET /api/duplicates/summary
        // Returns aggregate stats: how many duplicate groups, files, and bytes wasted.
        [HttpGet("summary")]
        public async Task<ActionResult<DuplicateSummaryDto>> GetSummary()
        {
            // Group by hash, keep only groups with more than one file.
            var groups = await _db.MediaFiles
                .GroupBy(f => f.ContentHash)
                .Where(g => g.Count() > 1)
                .Select(g => new
                {
                    Count = g.Count(),
                    SizeBytes = g.Max(f => f.SizeBytes) // size is the same per hash; max avoids arbitrary pick
                })
                .ToListAsync();

            int totalGroups = groups.Count;
            int totalDuplicateFiles = groups.Sum(g => g.Count);
            // Wasted bytes = (count - 1) copies × size per group
            long wastedBytes = groups.Sum(g => (long)(g.Count - 1) * g.SizeBytes);

            return Ok(new DuplicateSummaryDto(totalGroups, totalDuplicateFiles, wastedBytes));
        }

        // GET /api/duplicates/by-hash/{hash}
        // Returns all file locations that share the given SHA-256 hash.
        [HttpGet("by-hash/{hash}")]
        public async Task<ActionResult<DuplicateGroupDto>> GetByHash(string hash)
        {
            var files = await _db.MediaFiles
                .Include(f => f.Drive)
                .Where(f => f.ContentHash == hash)
                .OrderBy(f => f.Drive.Label)
                .ThenBy(f => f.RelativePath)
                .Select(f => new MediaFileDto(
                    f.Id,
                    f.Drive.Label,
                    f.RelativePath,
                    f.SizeBytes,
                    f.Extension,
                    f.ContentHash,
                    f.Category,
                    f.CreatedAtFs,
                    f.ModifiedAtFs))
                .ToListAsync();

            if (files.Count == 0)
                return NotFound();

            long sizeBytes = files[0].SizeBytes;
            return Ok(new DuplicateGroupDto(hash, sizeBytes, files));
        }
    }
}
