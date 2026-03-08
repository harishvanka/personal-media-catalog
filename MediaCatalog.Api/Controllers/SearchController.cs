using MediaCatalog.Api.Data;
using MediaCatalog.Api.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediaCatalog.Api.Controllers
{
    [ApiController]
    [Route("api/search")]
    public class SearchController : ControllerBase
    {
        private readonly MediaCatalogContext _db;

        public SearchController(MediaCatalogContext db) => _db = db;

        // GET /api/search/files?query=inception&category=Movie&driveLabel=4TB_Movies
        [HttpGet("files")]
        public async Task<ActionResult<List<MediaFileDto>>> SearchFiles(
            [FromQuery] string query,
            [FromQuery] string? category,
            [FromQuery] string? driveLabel)
        {
            if (string.IsNullOrWhiteSpace(query))
                return BadRequest("query parameter is required.");

            var q = _db.MediaFiles
                .Include(f => f.Drive)
                .Where(f => f.RelativePath.Contains(query));

            if (!string.IsNullOrWhiteSpace(category))
                q = q.Where(f => f.Category == category);

            if (!string.IsNullOrWhiteSpace(driveLabel))
                q = q.Where(f => f.Drive.Label == driveLabel);

            var results = await q
                .OrderBy(f => f.RelativePath)
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

            return Ok(results);
        }
    }
}
