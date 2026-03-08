using MediaCatalog.Api.Data;
using MediaCatalog.Api.Dtos;
using MediaCatalog.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediaCatalog.Api.Controllers
{
    [ApiController]
    [Route("api/organize")]
    public class OrganizeController : ControllerBase
    {
        private readonly MediaCatalogContext _db;
        private readonly IFileOrganizer _organizer;

        public OrganizeController(MediaCatalogContext db, IFileOrganizer organizer)
        {
            _db = db;
            _organizer = organizer;
        }

        // GET /api/organize/{fileId}/suggest
        [HttpGet("{fileId:int}/suggest")]
        public async Task<ActionResult<OrganizeSuggestionDto>> Suggest(int fileId)
        {
            var file = await _db.MediaFiles
                .Include(f => f.Drive)
                .FirstOrDefaultAsync(f => f.Id == fileId);

            if (file == null) return NotFound();

            var suggestedRel = _organizer.SuggestRelativePath(file);
            var currentPath  = Path.Combine(file.Drive.RootPath, file.RelativePath);
            var suggestedPath = Path.Combine(file.Drive.RootPath, suggestedRel);
            var alreadyOrganized = string.Equals(
                Path.GetFullPath(currentPath),
                Path.GetFullPath(suggestedPath),
                StringComparison.OrdinalIgnoreCase);

            return Ok(new OrganizeSuggestionDto(fileId, currentPath, suggestedPath, alreadyOrganized));
        }

        // POST /api/organize/{fileId}/move?dryRun=false
        [HttpPost("{fileId:int}/move")]
        public async Task<ActionResult<MoveResultDto>> Move(
            int fileId,
            [FromQuery] bool dryRun = false,
            CancellationToken ct = default)
        {
            var result = await _organizer.MoveAsync(fileId, dryRun, ct);

            if (!result.Success && result.Message == "File not found.")
                return NotFound();

            return Ok(result);
        }
    }
}
