using Microsoft.AspNetCore.Mvc;

namespace Shittim_Server.Controllers;

// Lobby banner art for Management_BannerList. Nothing in the pipeline serves files - the app is MapControllers plus /health - so the image needs a route.
// Catch-all because it is not settled whether the client fetches BannerDB.Url on its own or concatenates FileName onto it; both land here.
[ApiController]
public class BannerController : ControllerBase
{
    [HttpGet("/banner/{**path}")]
    public IResult Image()
    {
        var file = Path.Combine(AppContext.BaseDirectory, "Data", "Koyuki", "koyuki.png");
        if (!System.IO.File.Exists(file))
            return Results.NotFound();

        return Results.Bytes(System.IO.File.ReadAllBytes(file), "image/png");
    }
}
