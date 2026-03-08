using Microsoft.AspNetCore.Mvc;
using MediaCatalog.Api.Data;

namespace MediaCatalog.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly MediaCatalogContext _context;

        public HealthController(MediaCatalogContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var canConnect = await _context.Database.CanConnectAsync();
            return Ok(new { status = "OK", database = canConnect ? "available" : "unavailable" });
        }
    }
}