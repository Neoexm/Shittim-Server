using Microsoft.AspNetCore.Mvc;
using BlueArchiveAPI.Configuration;
using Shittim_Server.Services;

namespace Shittim_Server.Controllers
{
    [ApiController]
    [Route("addressables")]
    public class AddressablesController : ControllerBase
    {
        private readonly ModCatalogService _catalog;
        private readonly ILogger<AddressablesController> _logger;

        public AddressablesController(ModCatalogService catalog, ILogger<AddressablesController> logger)
        {
            _catalog = catalog;
            _logger = logger;
        }

        // The catalog sits under a path the client derives from the root it was handed and that layout moves between versions, so everything below the root is caught and sorted out by filename instead of being routed.
        [HttpGet("{**path}")]
        public IActionResult Get(string path)
        {
            if (string.IsNullOrEmpty(path))
                return NotFound();

            var file = Path.GetFileName(path);
            if (file.StartsWith("catalog_", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Catalog request: {Path}", path);
                var current = _catalog.Current();
                if (file.EndsWith(".hash", StringComparison.OrdinalIgnoreCase))
                    return Content(current.Hash, "text/plain");
                if (file.EndsWith(".bytes", StringComparison.OrdinalIgnoreCase))
                    return File(current.Bytes, "application/octet-stream");
            }

            if (path.StartsWith("mods/", StringComparison.OrdinalIgnoreCase))
            {
                var mods = Path.GetFullPath(CustomCharacterService.ModsDir);
                var full = Path.GetFullPath(Path.Combine(mods, path[5..].Replace('/', Path.DirectorySeparatorChar)));
                if (!full.StartsWith(mods, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(full))
                    return NotFound();
                return PhysicalFile(full, "application/octet-stream");
            }

            return Redirect($"{Config.Instance.ServerConfiguration.CdnBaseUrl}/{path}");
        }
    }
}
