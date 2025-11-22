#if DEBUG
using Microsoft.AspNetCore.Mvc;
using Shittim.Commands;
using Shittim.Services.WebClient;

namespace Shittim.Controllers.Dev
{
    [ApiController]
    [Route("dev")]
    public class CommandDevController : ControllerBase
    {
        private readonly WebService webService;

        public CommandDevController(WebService _webService)
        {
            webService = _webService;
        }

        [Route("command")]
        public ContentResult Command()
        {
            var html = @"
            <style>
                body { 
                    font-family: Arial, sans-serif; 
                    margin: 0; 
                    background-color: #2c2c2c;
                    color: #e0e0e0;
                }
                .container { 
                    max-width: 800px;
                    margin: 40px auto; 
                    padding: 30px;
                    border: 1px solid #555; 
                    border-radius: 8px; 
                    background-color: #333;
                    box-shadow: 0 4px 8px rgba(0, 0, 0, 0.5);
                }
                h2 {
                    color: #fff;
                    margin-top: 0;
                }
                .form-group { 
                    margin-bottom: 20px;
                }
                label { 
                    display: block; 
                    margin-bottom: 8px;
                    font-weight: bold; 
                    color: #c0c0c0;
                }
                input[type='number'], input[type='text'] { 
                    width: 100%; 
                    padding: 12px;
                    box-sizing: border-box; 
                    border: 1px solid #555;
                    border-radius: 4px;
                    background-color: #444;
                    color: #e0e0e0;
                    font-size: 16px;
                }
                button { 
                    padding: 12px 20px;
                    background-color: #007bff; 
                    color: white; 
                    border: none; 
                    border-radius: 4px; 
                    cursor: pointer; 
                    font-size: 16px;
                }
                button:hover { 
                    background-color: #0056b3; 
                }
                .result-output { 
                    background-color: #222;
                    border: 1px solid #444;
                    color: #e8e8e8;
                    padding: 15px;
                    margin-top: 20px;
                    border-radius: 4px;
                    white-space: pre-wrap;
                    word-wrap: break-word;
                    font-size: 14px;
                }
            </style>
            <div class='container'>
                <h2>Command Executor</h2>
                <form id='command-form'>
                    <div class='form-group'>
                        <label for='uid'>UID:</label>
                        <input type='number' id='uid' name='uid' required>
                    </div>
                    <div class='form-group'>
                        <label for='command'>Command:</label>
                        <input type='text' id='command' name='command' required>
                    </div>
                    <button type='submit'>Execute Command</button>
                </form>
                <pre class='result-output'></pre>
            </div>
            <script>
                document.getElementById('command-form').addEventListener('submit', async function(event) {
                    event.preventDefault();

                    const uid = document.getElementById('uid').value;
                    const command = document.getElementById('command').value;
                    const output = document.querySelector('.result-output');
                    output.textContent = 'Executing...';

                    try {
                        const response = await fetch('/dev/execute-command', {
                            method: 'POST',
                            headers: {
                                'Content-Type': 'application/json'
                            },
                            body: JSON.stringify({ uid: parseInt(uid), command: command })
                        });

                        if (response.ok) {
                            const result = await response.text();
                            output.textContent = result;
                        } else {
                            const error = await response.text();
                            output.textContent = 'Error: ' + error;
                        }
                    } catch (error) {
                        output.textContent = 'Network error: ' + error.message;
                    }
                });
            </script>";

            return new ContentResult
            {
                Content = html,
                ContentType = "text/html",
                StatusCode = 200
            };
        }

        [HttpPost("execute-command")]
        public async Task<IActionResult> ExecuteCommand([FromBody] CommandDevRequest request)
        {
            if (request == null || request.uid <= 0 || string.IsNullOrWhiteSpace(request.command))
            {
                return BadRequest("Invalid request. UID and command are required.");
            }

            var cmdStrings = request.command.Trim().Split(" ");
            var cmdName = cmdStrings.First().Split('/').Last();
            var cmdArgs = cmdStrings.Length > 1 ? cmdStrings[1..] : new string[0];

            try
            {
                using (var memoryStream = new MemoryStream())
                using (var memoryStreamWriter = new StreamWriter(memoryStream) { AutoFlush = true })
                {
                    var connection = webService.GetClient(request.uid, memoryStreamWriter);

                    Command? cmd = CommandFactory.CreateCommand(cmdName, connection, cmdArgs);

                    if (cmd == null)
                        return BadRequest($"Unknown command: {cmdName}");

                    await Task.Run(() => cmd.Execute());

                    memoryStream.Position = 0;
                    using var streamReader = new StreamReader(memoryStream);
                    var commandOutput = await streamReader.ReadToEndAsync();

                    return Ok(commandOutput);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing command for UID {request.uid}: {ex.Message}");
                return StatusCode(500, $"An error occurred while executing the command: {ex.Message}");
            }
        }

        public class CommandDevRequest
        {
            public long uid { get; set; }
            public string command { get; set; }
        }
    }
}
#endif
