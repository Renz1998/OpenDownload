using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using OpenDownload.Core;

if (args.Length >= 2 && args[0] == "--crash-child")
{
    var statePath = args[1];
    var childJob = JsonSerializer.Deserialize<DownloadJob>(File.ReadAllText(statePath))!;
    using var childEngine = new DownloadEngine();
    var transfer = childEngine.RunAsync(childJob, CancellationToken.None);
    while (!transfer.IsCompleted)
    {
        await Task.Delay(20);
        byte[] state = JsonSerializer.SerializeToUtf8Bytes(childJob);
        using (var output = new FileStream(statePath + ".new", FileMode.Create, FileAccess.Write)) { output.Write(state); output.Flush(true); }
        // Hosted Windows runners may briefly scan/lock a new checkpoint file.
        // Retry this fixture's atomic rename; never publish a partially-written snapshot.
        for (int attempt=0;;attempt++)
        {
            try { File.Move(statePath + ".new", statePath, true); break; }
            catch (Exception ex) when(attempt<20 && ex is IOException or UnauthorizedAccessException) { await Task.Delay(20); }
        }
    }
    await transfer; return;
}

string root = Path.GetFullPath(args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "../../../../../"));
string temp = Path.Combine(root, "test-results", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temp);
var info = new ProcessStartInfo(Environment.GetEnvironmentVariable("NODE_EXE") ?? "node") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
info.ArgumentList.Add(Path.Combine(root, "tests", "fixture.cjs"));
using var fixture = Process.Start(info)!;
int failed = 0, passed = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); }
async Task Test(string name, Func<Task> body)
{
    try { await body(); Console.WriteLine("PASS " + name); passed++; }
    catch (Exception ex) { Console.WriteLine("FAIL " + name + ": " + ex.Message); failed++; }
}
try
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(150));
    string? port = await fixture.StandardOutput.ReadLineAsync(timeout.Token);
    string origin = "http://127.0.0.1:" + int.Parse(port!);
    using var http = new HttpClient();
    using var meta = JsonDocument.Parse(await http.GetStringAsync(origin + "/meta"));
    string expected = meta.RootElement.GetProperty("sha256").GetString()!;
    DownloadJob Job(string path) => new() { Url = origin + path, FileName = "test.bin", Destination = Path.Combine(temp, Guid.NewGuid() + ".bin"), Connections = 4 };
    async Task Verify(DownloadJob job)
    {
        Check(job.Status == DownloadStatus.Completed, $"Status {job.Status}: {job.Error}");
        await using var stream = File.OpenRead(job.Destination);
        Check(Convert.ToHexString(await SHA256.HashDataAsync(stream)).Equals(expected, StringComparison.OrdinalIgnoreCase), "File hash mismatch");
    }
    await Test("segmented file and SHA-256", async () => { using var engine = new DownloadEngine(); var job = Job("/range"); await engine.RunAsync(job, timeout.Token); Check(job.Segments.Count == 4, "Not segmented"); await Verify(job); });
    await Test("query-string PNG keeps real filename and bytes", async () =>
    {
        var job = Job("/api/view?filename=NewScript_00123_.png&type=output&subfolder=");
        job.FileName = DownloadNames.FromUrl(new Uri(job.Url)); job.Destination = Path.Combine(temp, job.FileName);
        using var engine = new DownloadEngine(); await engine.RunAsync(job, timeout.Token);
        Check(job.Status == DownloadStatus.Completed && job.FileName == "NewScript_00123_.png", "Query filename was lost");
        Check(FileFormats.ImageExtension(await File.ReadAllBytesAsync(job.Destination)) == ".png", "PNG bytes changed");
        Check(job.StartedAt.HasValue && job.FinishedAt >= job.StartedAt && job.ActiveSeconds > 0, "Completion timing missing");
    });
    await Test("image signature corrects misleading MIME extension", async () =>
    {
        var job = Job("/image-mislabel"); using var engine = new DownloadEngine(); await engine.RunAsync(job, timeout.Token);
        Check(job.Status == DownloadStatus.Completed && Path.GetExtension(job.FileName) == ".png" && File.Exists(job.Destination), "Signature did not correct the filename");
    });
    await Test("Content-Disposition respects collisions and explicit names", async () =>
    {
        string existing = Path.Combine(temp, "report final.zip"); await File.WriteAllTextAsync(existing,"keep");
        var job = Job("/named"); using var engine = new DownloadEngine(); await engine.RunAsync(job,timeout.Token); await Verify(job);
        Check(job.FileName == "report final (1).zip" && await File.ReadAllTextAsync(existing) == "keep", "Server filename overwrote a file");
        var custom = Job("/named"); custom.AutoFileName = false; string original = custom.Destination;
        await engine.RunAsync(custom,timeout.Token); await Verify(custom); Check(custom.Destination == original, "Custom name changed");
    });
    await Test("server ignores Range: single stream fallback", async () => { using var engine = new DownloadEngine(); var job = Job("/ignore"); await engine.RunAsync(job, timeout.Token); Check(job.Segments.Count == 1, "Unexpected segmentation"); await Verify(job); });
    await Test("chunked response with unknown length", async () => { using var engine = new DownloadEngine(); var job = Job("/unknown"); await engine.RunAsync(job, timeout.Token); await Verify(job); });
    await Test("no validator uses one stream", async () => { using var engine = new DownloadEngine(); var job = Job("/novalidator"); await engine.RunAsync(job, timeout.Token); Check(!job.SupportsRanges, "Unsafe ranges"); await Verify(job); });
    await Test("pause, serialize, fresh-engine resume", async () =>
    {
        var job = Job("/slow"); using var pause = new CancellationTokenSource(160);
        using (var engine = new DownloadEngine()) await engine.RunAsync(job, pause.Token);
        Check(job.Status == DownloadStatus.Paused && job.Downloaded > 0 && job.Downloaded < job.TotalBytes, "Pause did not retain partial progress");
        job = JsonSerializer.Deserialize<DownloadJob>(JsonSerializer.Serialize(job))!;
        double pausedSeconds = job.ActiveSeconds; var firstStart = job.StartedAt;
        using (var engine = new DownloadEngine()) await engine.RunAsync(job, timeout.Token);
        await Verify(job);
        Check(job.StartedAt == firstStart && job.FinishedAt.HasValue && job.ActiveSeconds > pausedSeconds, "Resume did not retain timing");
    });
    await Test("changed ETag refuses unsafe resume", async () =>
    {
        var job = Job("/change"); using var pause = new CancellationTokenSource(160);
        using var engine = new DownloadEngine(); await engine.RunAsync(job, pause.Token); long before = job.Downloaded;
        await http.GetStringAsync(origin + "/mutate"); await engine.RunAsync(job, timeout.Token);
        Check(job.Status == DownloadStatus.Failed && job.Downloaded == before && !File.Exists(job.Destination), "Source change was not rejected");
        DownloadEngine.Restart(job); await engine.RunAsync(job, timeout.Token); await Verify(job);
    });
    await Test("invalid Content-Range rejected", async () => { using var engine = new DownloadEngine(); var job = Job("/bad"); await engine.RunAsync(job, timeout.Token); Check(job.Status == DownloadStatus.Failed && !File.Exists(job.Destination), "Invalid range accepted"); });
    await Test("invalid transfer range after valid probe rejected", async () => { using var engine = new DownloadEngine(); var job = Job("/badlate"); await engine.RunAsync(job, timeout.Token); Check(job.Status == DownloadStatus.Failed && !File.Exists(job.Destination), "Invalid transfer range accepted"); });
    await Test("hard process crash resumes durable checkpoint", async () =>
    {
        var job = Job("/slow"); string state = Path.Combine(temp, "crash-state.json"); await File.WriteAllTextAsync(state, JsonSerializer.Serialize(job));
        var childInfo = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        childInfo.ArgumentList.Add("--crash-child"); childInfo.ArgumentList.Add(state);
        using var child = Process.Start(childInfo)!;
        try
        {
            for (int i = 0; i < 80; i++)
            {
                await Task.Delay(25, timeout.Token);
                try
                {
                    // The writer atomically replaces this file; readers must allow that rename on Windows.
                    await using var checkpoint = new FileStream(state, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    job = (await JsonSerializer.DeserializeAsync<DownloadJob>(checkpoint, cancellationToken: timeout.Token))!;
                }
                catch (IOException) { continue; }
                if (job.Downloaded >= 256 * 1024) break;
                if (child.HasExited) throw new Exception("Crash child exited early: " + await child.StandardError.ReadToEndAsync());
            }
            Check(job.Downloaded > 0 && job.Downloaded < job.TotalBytes, "Missing partial checkpoint");
        }
        finally { if (!child.HasExited) child.Kill(true); await child.WaitForExitAsync(); }
        job = JsonSerializer.Deserialize<DownloadJob>(await File.ReadAllTextAsync(state))!;
        using var engine = new DownloadEngine(); await engine.RunAsync(job, timeout.Token); await Verify(job);
    });
    await Test("connection reset retries without corruption", async () => { using var engine = new DownloadEngine(); var job = Job("/drop"); await engine.RunAsync(job, timeout.Token); await Verify(job); });
    await Test("HTTP redirect", async () => { using var engine = new DownloadEngine(); var job = Job("/redirect"); await engine.RunAsync(job, timeout.Token); await Verify(job); });
    await Test("cross-origin redirect strips credentials", async () => { using var engine = new DownloadEngine(); var job = Job("/cross-auth"); job.Headers["Authorization"] = "Bearer secret"; job.Headers["Cookie"] = "session=secret"; await engine.RunAsync(job, timeout.Token); await Verify(job); });
    await Test("authentication header", async () => { using var engine = new DownloadEngine(); var job = Job("/auth"); job.Headers["Authorization"] = "Bearer test-token"; await engine.RunAsync(job, timeout.Token); await Verify(job); });
    await Test("403 preserves failure state", async () => { using var engine = new DownloadEngine(); var job = Job("/forbidden"); await engine.RunAsync(job, timeout.Token); Check(job.Status == DownloadStatus.Failed && job.Error!.Contains("403"), "Missing HTTP failure"); });
    await Test("empty file", async () => { using var engine = new DownloadEngine(); var job = Job("/empty"); await engine.RunAsync(job, timeout.Token); Check(job.Status == DownloadStatus.Completed && new FileInfo(job.Destination).Length == 0, "Empty file failed"); });
    await Test("existing destination never overwritten", async () => { using var engine = new DownloadEngine(); var job = Job("/range"); await File.WriteAllTextAsync(job.Destination, "keep"); await engine.RunAsync(job, timeout.Token); Check(job.Status == DownloadStatus.Failed && await File.ReadAllTextAsync(job.Destination) == "keep", "Existing file changed"); });
    await Test("aggregate speed limit", async () =>
    {
        using var limiter = new DownloadEngine(); limiter.Limiter.LimitKiB = () => 128; var watch = Stopwatch.StartNew();
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => limiter.Limiter.WaitAsync(32768, timeout.Token)));
        Check(watch.Elapsed.TotalSeconds >= .9 && watch.Elapsed.TotalSeconds < 5, "Global cap not respected");
    });
    await Test("scheduler wraps midnight", () => { var s = new Settings { ScheduleEnabled = true, ScheduleStartMinute = 23 * 60, ScheduleEndMinute = 6 * 60 }; Check(s.AllowedNow(new DateTime(2026, 1, 1, 1, 0, 0)), "Night excluded"); Check(!s.AllowedNow(new DateTime(2026, 1, 1, 12, 0, 0)), "Day included"); return Task.CompletedTask; });
    await Test("safe names and collision avoidance", () => { Check(DownloadNames.Sanitize("../../CON.exe") == "_CON.exe", "Reserved name"); Check(DownloadNames.Sanitize("foo\\bar.zip") == "bar.zip", "Traversal"); string target = DownloadNames.ReservePath(temp, "name.bin", [Path.Combine(temp, "name.bin")]); Check(target.EndsWith("name (1).bin"), "Collision"); bool denied = false; try { DownloadNames.ValidateUrl("file:///C:/test"); } catch (ArgumentException) { denied = true; } Check(denied, "Non-HTTP accepted"); return Task.CompletedTask; });
}
finally { if (!fixture.HasExited) fixture.Kill(true); }
Console.WriteLine($"{passed} passed; {failed} failed. Artifacts: {temp}");
Environment.ExitCode = failed == 0 ? 0 : 1;
