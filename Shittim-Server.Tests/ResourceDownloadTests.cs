using System.Net;
using System.Net.Sockets;
using System.Text;
using BlueArchiveAPI.Services;
using Xunit;

namespace Shittim_Server.Tests;

// The resource download is the longest thing the server does before it will accept a login, and it is the only part of
// startup with nothing on the far end under our control. A transfer that has quietly died and one that is still moving
// are the same thing from the console if neither of them prints anything.
public class ResourceDownloadTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"shittim-dl-{Guid.NewGuid():N}");
    private readonly TextWriter _originalOut = Console.Out;
    private readonly TimeSpan _originalStall = ResourceService.StallTimeout;

    public ResourceDownloadTests()
    {
        Directory.CreateDirectory(_dir);
        ResourceService.StallTimeout = TimeSpan.FromSeconds(2);
    }

    // A CDN that accepts the connection, sends a Content-Length and then stops writing holds the socket open for as long
    // as it likes. Waiting that out is indistinguishable from a working download of a very large file.
    [Fact]
    public async Task ASocketThatGoesQuietIsGivenUpOnAndSaysSo()
    {
        using var cdn = new FakeCdn(declared: 4 * 1024 * 1024, sending: 64 * 1024);
        var output = Path.Combine(_dir, "ExcelDB.db");

        var download = ResourceService.DownloadFile(cdn.Url, output);
        var first = await Task.WhenAny(download, Task.Delay(TimeSpan.FromSeconds(20)));

        Assert.Same(download, first);
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => download);
        Assert.Contains("ExcelDB.db", ex.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
        Assert.False(File.Exists(output + ".part"));
    }

    [Fact]
    public async Task ProgressIsPrintedWhileTheBodyIsStillArriving()
    {
        using var cdn = new FakeCdn(declared: 512 * 1024, sending: 512 * 1024);
        var captured = new StringWriter();
        Console.SetOut(TextWriter.Synchronized(captured));

        await ResourceService.DownloadFile(cdn.Url, Path.Combine(_dir, "Excel.zip"));

        Console.SetOut(_originalOut);
        var progress = captured.ToString().Split('\n').Where(x => x.Contains("Excel.zip", StringComparison.Ordinal) && x.Contains('%')).ToList();
        Assert.True(progress.Count >= 3, captured.ToString());
    }

    // The point of measuring the gap rather than the whole transfer: ExcelDB.db is large enough that any total deadline
    // short enough to catch a dead socket is also short enough to kill a working download on a slow line.
    [Fact]
    public async Task ASlowDownloadThatKeepsArrivingIsNotGivenUpOn()
    {
        using var cdn = new FakeCdn(declared: 128 * 1024, sending: 128 * 1024, pace: TimeSpan.FromMilliseconds(600));
        var output = Path.Combine(_dir, "ExcelDB.db");

        await ResourceService.DownloadFile(cdn.Url, output);

        Assert.Equal(128 * 1024, new FileInfo(output).Length);
    }

    // A body cut off short of its Content-Length is a truncated archive, and the extractor's error for one of those says
    // nothing about the download.
    [Fact]
    public async Task ABodyThatEndsEarlyIsNotAcceptedAsTheFile()
    {
        using var cdn = new FakeCdn(declared: 512 * 1024, sending: 100 * 1024, closeAfterSending: true);
        var output = Path.Combine(_dir, "HexaMap.zip");

        await Assert.ThrowsAnyAsync<Exception>(() => ResourceService.DownloadFile(cdn.Url, output));

        Assert.False(File.Exists(output));
        Assert.False(File.Exists(output + ".part"));
    }

    // Downloaded/ExcelDB.db from a run that worked is what the size check compares against and what gets copied into
    // Dumped, so a re-download that fails must not be the thing that destroys it.
    [Fact]
    public async Task AFileFromAnEarlierRunSurvivesAFailedDownload()
    {
        using var cdn = new FakeCdn(declared: 512 * 1024, sending: 100 * 1024, closeAfterSending: true);
        var output = Path.Combine(_dir, "ExcelDB.db");
        var kept = new byte[200 * 1024];
        Array.Fill(kept, (byte)0xAB);
        File.WriteAllBytes(output, kept);

        await Assert.ThrowsAnyAsync<Exception>(() => ResourceService.DownloadFile(cdn.Url, output));

        Assert.Equal(kept, File.ReadAllBytes(output));
    }

    // Accepting the connection and then never answering is the other half of the stall: no headers arrive, so nothing the
    // read loop does can help.
    [Fact]
    public async Task AServerThatAcceptsTheConnectionAndNeverAnswersIsGivenUpOn()
    {
        using var cdn = new FakeCdn(declared: 0, sending: 0, silent: true);
        var output = Path.Combine(_dir, "Excel.zip");

        var download = ResourceService.DownloadFile(cdn.Url, output);
        var first = await Task.WhenAny(download, Task.Delay(TimeSpan.FromSeconds(20)));

        Assert.Same(download, first);
        await Assert.ThrowsAnyAsync<Exception>(() => download);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public async Task AFileAlreadyOnDiskAtTheAdvertisedSizeIsNotFetchedAgain()
    {
        using var cdn = new FakeCdn(declared: 4096, sending: 4096);
        var output = Path.Combine(_dir, "ExcelDB.db");
        var existing = new byte[4096];
        Array.Fill(existing, (byte)0xAB);
        File.WriteAllBytes(output, existing);

        await ResourceService.DownloadFile(cdn.Url, output);

        Assert.Equal(existing, File.ReadAllBytes(output));
    }

    public void Dispose()
    {
        Console.SetOut(_originalOut);
        ResourceService.StallTimeout = _originalStall;

        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    // HttpListener wants a URL ACL for anything but the default prefixes, so the responses are written by hand. It answers
    // one HEAD and then one GET, on separate connections because the HEAD reply closes.
    private sealed class FakeCdn : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly long _declared;
        private readonly int _sending;
        private readonly bool _closeAfterSending;
        private readonly TimeSpan _pace;
        private readonly bool _silent;

        public FakeCdn(long declared, int sending, bool closeAfterSending = false, TimeSpan pace = default, bool silent = false)
        {
            _declared = declared;
            _sending = sending;
            _closeAfterSending = closeAfterSending;
            _pace = pace;
            _silent = silent;

            _listener.Start();
            Url = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/Preload/TableBundles/resource";
            _ = Task.Run(Serve);
        }

        public string Url { get; }

        private async Task Serve()
        {
            while (!_stop.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(_stop.Token);
                }
                catch
                {
                    return;
                }

                _ = Task.Run(() => Respond(client));
            }
        }

        private async Task Respond(TcpClient client)
        {
            try
            {
                using (client)
                {
                    var stream = client.GetStream();
                    var request = new byte[8192];
                    var read = await stream.ReadAsync(request, _stop.Token);
                    var head = Encoding.ASCII.GetString(request, 0, read).StartsWith("HEAD", StringComparison.Ordinal);

                    if (_silent && !head)
                    {
                        await Task.Delay(Timeout.Infinite, _stop.Token);
                        return;
                    }

                    var header = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {_declared}\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(header, _stop.Token);
                    if (head)
                        return;

                    var chunk = new byte[16 * 1024];
                    for (var sent = 0; sent < _sending; sent += chunk.Length)
                    {
                        await stream.WriteAsync(chunk.AsMemory(0, Math.Min(chunk.Length, _sending - sent)), _stop.Token);
                        if (_pace > TimeSpan.Zero)
                        {
                            await stream.FlushAsync(_stop.Token);
                            await Task.Delay(_pace, _stop.Token);
                        }
                    }

                    if (_closeAfterSending)
                        return;

                    await Task.Delay(Timeout.Infinite, _stop.Token);
                }
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            _stop.Cancel();
            _listener.Stop();
            _stop.Dispose();
        }
    }
}
