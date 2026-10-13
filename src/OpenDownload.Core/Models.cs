using System.Text.Json.Serialization;

namespace OpenDownload.Core;

public enum DownloadStatus { Queued, Connecting, Downloading, Paused, Completed, Failed }
public sealed class Segment
{
    public long Start { get; set; }
    public long End { get; set; }
    [JsonInclude] public long Committed;
    [JsonIgnore] public long Length => End - Start + 1;
}
public sealed class DownloadJob
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public bool Hidden { get; set; }
    public string Url { get; set; } = "";
    public string FileName { get; set; } = "";
    public string Destination { get; set; } = "";
    public DownloadStatus Status { get; set; } = DownloadStatus.Queued;
    public long? TotalBytes { get; set; }
    public string? Validator { get; set; }
    public bool SupportsRanges { get; set; }
    public List<Segment> Segments { get; set; } = [];
    public string? Error { get; set; }
    public DateTimeOffset Added { get; set; } = DateTimeOffset.Now;
    public string? Sha256 { get; set; }
    public int Connections { get; set; } = 4;
    public string? Referer { get; set; }
    [JsonIgnore] public Dictionary<string, string> Headers { get; set; } = [];
    [JsonIgnore] public long Downloaded => Segments.Sum(s => Interlocked.Read(ref s.Committed));
    [JsonIgnore] public string PartialPath => Destination + "." + Id + ".odpart";
    [JsonIgnore] public double BytesPerSecond { get; set; }
    [JsonIgnore] public string Category => Path.GetExtension(FileName).ToLowerInvariant() switch
    {
        ".safetensors" or ".gguf" or ".ckpt" or ".pt" => "AI models",
        ".mp4" or ".mkv" or ".webm" or ".mov" or ".avi" => "Video",
        ".mp3" or ".wav" or ".flac" or ".ogg" => "Music",
        ".zip" or ".7z" or ".rar" or ".gz" => "Archives",
        ".exe" or ".msi" => "Programs",
        ".pdf" or ".docx" or ".txt" or ".epub" => "Documents",
        _ => "Other"
    };
}
public sealed class Settings
{
    public string? Layout { get; set; }
    public bool LightTheme { get; set; }
    public bool CompactRows { get; set; }
    public int GamingLimitKiB { get; set; } = 5120;
    public int? PreviousSpeedLimitKiB { get; set; }
    public DateTimeOffset? BrowserCheckedAt { get; set; }
    public string DownloadDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "OpenDownload");
    public int Connections { get; set; } = 4;
    public int ConcurrentDownloads { get; set; } = 2;
    public int SpeedLimitKiB { get; set; }
    public bool ScheduleEnabled { get; set; }
    public int ScheduleStartMinute { get; set; } = 0;
    public int ScheduleEndMinute { get; set; } = 360;
    public bool MinimizeToTray { get; set; } = true;
    public bool AllowedNow(DateTime now)
    {
        if (!ScheduleEnabled) return true;
        int minute = now.Hour * 60 + now.Minute;
        return ScheduleStartMinute == ScheduleEndMinute || (ScheduleStartMinute < ScheduleEndMinute
            ? minute >= ScheduleStartMinute && minute < ScheduleEndMinute
            : minute >= ScheduleStartMinute || minute < ScheduleEndMinute);
    }
}
public static class DownloadNames
{
    public static Uri ValidateUrl(string value)
    {
        if (value.Length > 16384 || !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "https" && uri.Scheme != "http") || !string.IsNullOrEmpty(uri.UserInfo))
            throw new ArgumentException("Enter an HTTP or HTTPS URL without an embedded username or password.");
        return uri;
    }
    public static string Sanitize(string name)
    {
        name = name.Replace('\\', '/').Split('/').Last();
        name = new string(name.Select(c => c < 32 || "<>:\"/\\|?*".Contains(c) ? '_' : c).ToArray()).Trim().TrimEnd('.');
        if (string.IsNullOrWhiteSpace(name)) name = "download.bin";
        string stem = name.Split('.')[0];
        if (new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }.Contains(stem, StringComparer.OrdinalIgnoreCase)) name = "_" + name;
        return name.Length > 180 ? name[..150] + Path.GetExtension(name)[..Math.Min(25, Path.GetExtension(name).Length)] : name;
    }
    public static string FromUrl(Uri uri)
    {
        try { return Sanitize(Uri.UnescapeDataString(uri.AbsolutePath.Split('/').Last())); }
        catch { return "download.bin"; }
    }
    public static string ReservePath(string directory, string name, IEnumerable<string> reserved)
    {
        directory = Path.GetFullPath(directory);
        var taken = reserved.ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < 10000; i++)
        {
            string candidate = Path.Combine(directory, i == 0 ? name : $"{Path.GetFileNameWithoutExtension(name)} ({i}){Path.GetExtension(name)}");
            if (!File.Exists(candidate) && !taken.Contains(candidate)) return candidate;
        }
        throw new IOException("Cannot find an unused file name.");
    }
}
