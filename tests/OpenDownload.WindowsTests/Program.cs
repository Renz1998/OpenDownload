using System.Security.Cryptography;
using OpenDownload.Core;
using OpenDownload.Desktop;

if (args.Length > 1 && args[0] == "--inspect")
{
    using var database = new Store(args[1]);
    foreach (var saved in database.Load()) Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { saved.FileName, saved.Destination, saved.Status, saved.Error, saved.TotalBytes, saved.Downloaded }));
    return;
}

var folder = Path.Combine(Path.GetFullPath(args.Length > 0 ? args[0] : "."), "test-results", "store-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);
int count = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); count++; }
var job = new DownloadJob { Url = "https://example.invalid/file?secret=signed-test-link", FileName = "file.bin", Destination = Path.Combine(folder, "file.bin"), Status = DownloadStatus.Downloading,
    Segments = [new Segment { Start = 0, End = 9, Committed = 5 }], TotalBytes = 10, Headers = new Dictionary<string, string> { ["Authorization"] = "Bearer secret-test-token" } };
var settings = new Settings { DownloadDirectory = folder, SpeedLimitKiB = 256, ScheduleEnabled = true };
using (var store = new Store(folder))
{
    store.Save([job], settings);
    var copy = store.Load().Single();
    Check(copy.Status == DownloadStatus.Paused && copy.Downloaded == 5, "active state recovers paused with committed offsets");
    Check(copy.Headers["Authorization"] == "Bearer secret-test-token" && copy.Url == job.Url, "DPAPI authentication and URL round trip");
    Check(store.LoadSettings().DownloadDirectory == folder && store.LoadSettings().SpeedLimitKiB == 256, "settings persist");
}
string contents = System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(folder, "downloads.db")));
Check(!contents.Contains("secret-test-token") && !contents.Contains("signed-test-link"), "database does not contain plaintext URL or token");
File.WriteAllText(job.Destination, "completed");
job.Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(job.Destination))).ToLowerInvariant();
using (var store = new Store(folder))
{
    store.Save([job], settings);
    Check(store.Load().Single().Status == DownloadStatus.Completed, "crash between rename and final save recovers completion by hash");
    File.WriteAllText(job.Destination, "tampered");
    Check(store.Load().Single().Status == DownloadStatus.Paused, "hash mismatch never marks recovery completed");
    store.Remove(job.Id); Check(store.Load().Count == 0 && File.Exists(job.Destination), "history removal keeps downloaded file");
}
Console.WriteLine($"{count} Windows persistence tests passed.");
