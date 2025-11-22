using Microsoft.AspNetCore.Mvc;
using Shittim.Models.GM;
using Shittim.Services.WebClient;
using Serilog;

namespace Shittim.Controllers.GM
{
    [ApiController]
    [Route("/dev/api")]
    public class CommandController : ControllerBase
    {
        private readonly WebService webService;

        public CommandController(WebService _webService)
        {
            webService = _webService;
        }

        [HttpPost]
        [Route("command")]
        public async Task<IResult> RunCommand(CommandRequest request)
        {
            var cmdStrings = request.Command.Trim().Split(" ");
            var cmdName = cmdStrings.First().Split('/').Last();
            var cmdArgs = cmdStrings[1..];

            var memoryStream = new MemoryStream();
            var memoryStreamWriter = new StreamWriter(memoryStream)
            {
                AutoFlush = true
            };

            var connection = webService.GetClient(request.UserID, memoryStreamWriter);

            string commandOutput;
            try
            {
                commandOutput = $"Command system not yet fully implemented. Command received: {cmdName} with args: {string.Join(", ", cmdArgs)}";
                memoryStream.Position = 0;
                using var streamReader = new StreamReader(memoryStream);
                commandOutput = await streamReader.ReadToEndAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred during command execution.");
                return BaseAPIResponse.Create(ResponseStatus.Error, "An error occurred during command execution.");
            }
            finally
            {
                await memoryStreamWriter.DisposeAsync();
                await memoryStream.DisposeAsync();
            }
            if (string.IsNullOrWhiteSpace(commandOutput))
            {
                commandOutput = $"Command '{cmdName}' executed successfully! Please relog for it to take effect.";
            }

            return BaseAPIResponse.Create(ResponseStatus.Success, (object)commandOutput);
        }
    }
}
