using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using OpenDownload.Core;

// stdout is exclusively the browser's length-prefixed messaging channel.
var input = Console.OpenStandardInput(); var output = Console.OpenStandardOutput();
try
{
    while (true)
    {
        var length = new byte[4]; int first = await input.ReadAsync(length.AsMemory(0, 1)); if (first == 0) break;
        await input.ReadExactlyAsync(length.AsMemory(1, 3));
        int size = BinaryPrimitives.ReadInt32LittleEndian(length);
        if (size is < 2 or > 128 * 1024) throw new IOException("Invalid message size");
        var payload = new byte[size]; await input.ReadExactlyAsync(payload);
        object reply;
        try
        {
            using var doc = JsonDocument.Parse(payload);
            string action = doc.RootElement.TryGetProperty("action", out var a) ? a.GetString() ?? "" : "download";
            if (action is not ("ping" or "browser-status"))
            {
                var url = doc.RootElement.GetProperty("url").GetString();
                if (url?.Length > 16384 || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || uri.UserInfo.Length != 0) throw new IOException("Invalid URL");
            }
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            await using var pipe = new NamedPipeClientStream(".", AppPaths.Pipe(AppPaths.DataFolder(AppContext.BaseDirectory)), PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            try { await pipe.ConnectAsync(500, timeout.Token); }
            catch (TimeoutException)
            {
                if(action=="browser-status") throw new IOException("Desktop is not running");
                var app = Path.Combine(AppContext.BaseDirectory, "OpenDownload.Desktop.exe");
                if (!File.Exists(app)) app = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "desktop", "OpenDownload.Desktop.exe"));
                if (!File.Exists(app)) throw new IOException("Desktop app is missing");
                var info = new ProcessStartInfo(app) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(app)! };
                var config = Path.Combine(AppContext.BaseDirectory, "data-path.txt");
                if (File.Exists(config)) info.Environment["OPENDOWNLOAD_DATA"] = File.ReadAllText(config).Trim();
                Process.Start(info); await pipe.ConnectAsync(15000, timeout.Token);
            }
            await pipe.WriteAsync(payload, timeout.Token); await pipe.WriteAsync(new byte[] { 10 }, timeout.Token); await pipe.FlushAsync(timeout.Token);
            using var reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, true);
            var result = await reader.ReadLineAsync(timeout.Token);
            reply = JsonSerializer.Deserialize<JsonElement>(result ?? "{\"ok\":false}");
        }
        catch { reply = new { ok = false, error = "Could not add the download. Check browser registration, download folder and that OpenDownload can start." }; }
        var encoded = JsonSerializer.SerializeToUtf8Bytes(reply);
        BinaryPrimitives.WriteInt32LittleEndian(length, encoded.Length);
        await output.WriteAsync(length); await output.WriteAsync(encoded); await output.FlushAsync();
    }
}
catch { Environment.ExitCode = 1; }
