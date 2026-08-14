using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Shittim_Server.Services;

namespace Shittim_Server.Controllers;

[ApiController]
[Route("api/admin/mods")]
[AdminAuth]
public class ModsController : ControllerBase
{
    private readonly CustomCharacterService _characters;
    private readonly ModCatalogService _catalog;

    public ModsController(CustomCharacterService characters, ModCatalogService catalog)
    {
        _characters = characters;
        _catalog = catalog;
    }

    [HttpGet("characters")]
    public IActionResult Characters()
    {
        try
        {
            var dbs = CustomCharacterService.DatabasePaths();
            return Ok(new { databases = dbs.Count, characters = _characters.List() });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("characters/inspect")]
    public IActionResult Inspect([FromBody] InspectZipRequest request)
    {
        try
        {
            return Ok(_characters.Inspect(request.ZipPath));
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("characters/import")]
    public IActionResult Import([FromBody] ImportCharacterRequest request)
    {
        try
        {
            var name = string.IsNullOrWhiteSpace(request.Name) ? Path.GetFileNameWithoutExtension(request.ZipPath) : request.Name.Trim();
            var result = _characters.Import(request.ZipPath, request.DonorId, request.Id, name, request.Overrides);
            _catalog.SyncClient();
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("characters/{id:long}")]
    public IActionResult Detail(long id)
    {
        try
        {
            return Ok(_characters.Detail(id));
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("characters/{id:long}/update")]
    public IActionResult Update(long id, [FromBody] UpdateCharacterRequest request)
    {
        try
        {
            _characters.Update(id, request.Name, request.Character, request.Profile, request.Stat);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("characters/{id:long}/remove")]
    public IActionResult Remove(long id)
    {
        try
        {
            _characters.Remove(id);
            _catalog.SyncClient();
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public class InspectZipRequest
{
    public string ZipPath { get; set; }
}

public class ImportCharacterRequest
{
    public string ZipPath { get; set; }
    public long DonorId { get; set; }
    public long? Id { get; set; }
    public string Name { get; set; }
    public Dictionary<string, JsonElement> Overrides { get; set; }
}

public class UpdateCharacterRequest
{
    public string Name { get; set; }
    public Dictionary<string, JsonElement> Character { get; set; }
    public Dictionary<string, JsonElement> Profile { get; set; }
    public Dictionary<string, JsonElement> Stat { get; set; }
}
