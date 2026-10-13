using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Windows;
using OpenDownload.Core;

namespace OpenDownload.Desktop;

public sealed class BrowserRequest
{
    public string Action { get; set; } = "download";
    public string Url { get; set; } = "";
    public string? FileName { get; set; }
    public string? Referer { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
    public string? ExtensionId { get; set; }
}
public partial class App : Application
{
    private Mutex? instance;
    private readonly CancellationTokenSource shutdown = new();
    public static string DataDirectory { get; private set; } = "";
    public static string PipeName => AppPaths.Pipe(DataDirectory);
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DataDirectory = AppPaths.DataFolder(AppContext.BaseDirectory);
        instance = new Mutex(true, "Local\\" + PipeName, out bool first);
        if (!first)
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.ConnectAsync(4000, timeout.Token);
                await pipe.WriteAsync(System.Text.Encoding.UTF8.GetBytes("{\"action\":\"show\"}\n"), timeout.Token);
                await pipe.FlushAsync(timeout.Token);
                await ReadBoundedLineAsync(pipe, 1024, timeout.Token);
            }
            catch { /* Already-running instance may be busy; never start a second writer. */ }
            Shutdown(); return;
        }
        Directory.CreateDirectory(DataDirectory);
        DispatcherUnhandledException += (_, error) =>
        {
            File.WriteAllText(Path.Combine(DataDirectory, "last-error.txt"), error.Exception.GetType().Name + "\n" + error.Exception.StackTrace);
        };
        try
        {
            var main = new MainWindow(); MainWindow = main; main.Show();
            _ = ListenAsync(main, shutdown.Token);
            if (e.Args.Length >= 2 && e.Args[0] == "--ui-self-test" && Environment.GetEnvironmentVariable("OPENDOWNLOAD_TEST_MODE") == "1")
                main.ContentRendered += async (_, _) => await main.RunUiTestsAsync(e.Args[1]);
        }
        catch (Exception)
        {
            MessageBox.Show("OpenDownload could not open its local database. Check free space and permissions, and keep the data directory on this Windows account. Your downloads and database have not been deleted.", "OpenDownload startup"); Shutdown(1);
        }
    }
    private async Task ListenAsync(MainWindow window, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(ct);
                using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                requestTimeout.CancelAfter(TimeSpan.FromSeconds(10));
                var line = await ReadBoundedLineAsync(pipe, 128 * 1024, requestTimeout.Token);
                var req = JsonSerializer.Deserialize<BrowserRequest>(line, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new IOException("Empty request");
                bool accepted = false;
                await Dispatcher.InvokeAsync(() =>
                {
                    if (req.Action == "ping") { if (req.ExtensionId == AppPaths.ExtensionId) window.RecordBrowserCheck(); accepted = true; }
                    else if (req.Action == "show") { window.ShowFromRequest(); accepted = true; }
                    else if (req.Action == "exit-for-update") { accepted = true; _ = window.ExitForUpdateAsync(); }
                    else
                    {
                        try { DownloadNames.ValidateUrl(req.Url); window.ReceiveBrowserRequest(req); accepted = true; }
                        catch { accepted = false; }
                    }
                });
                var reply = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { ok = accepted }) + "\n");
                await pipe.WriteAsync(reply, requestTimeout.Token);
            }
            catch (Exception) when (!ct.IsCancellationRequested) { /* Invalid requests do not terminate the inbox. */ }
            catch (OperationCanceledException) { break; }
        }
    }
    private static async Task<string> ReadBoundedLineAsync(Stream stream, int limit, CancellationToken ct)
    {
        using var bytes = new MemoryStream(); var buffer = new byte[1];
        while (bytes.Length < limit)
        {
            if (await stream.ReadAsync(buffer, ct) == 0) throw new EndOfStreamException();
            if (buffer[0] == 10) return System.Text.Encoding.UTF8.GetString(bytes.ToArray());
            bytes.WriteByte(buffer[0]);
        }
        throw new IOException("Request too large");
    }
    protected override void OnExit(ExitEventArgs e)
    { shutdown.Cancel(); instance?.Dispose(); base.OnExit(e); }
}
